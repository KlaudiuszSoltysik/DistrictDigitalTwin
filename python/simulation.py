from json import dumps, loads
from os import getenv
from threading import Lock, Thread, Event
from time import sleep, time

from dotenv import load_dotenv
from pika import BlockingConnection, URLParameters, DeliveryMode, BasicProperties

from shared.DistrictSimulation import DistrictSimulation
from shared.MongoDbController import MongoDbController
from shared.logger_config import setup_logger


class SimulationService:
    def __init__(self):
        self.logger = setup_logger("simulation")
        self.mongodb = MongoDbController()

        amqp_url = getenv("RABBITMQ_CONNECTION_STRING")
        self.rabbit_params = URLParameters(amqp_url)

        self.simulation = DistrictSimulation("config/weather_history.csv")

        self.lock = Lock()
        self.wake_event = Event()

        self.run_id = int(time())
        self.is_paused = True
        self.simulation_speed = 30
        self.simulation_step = 300
        self.room_temperature_noise_sigma = 0.1

    def start(self):
        listener_thread = Thread(target=self.listen_for_commands, daemon=True)
        listener_thread.start()

        self.run_physics_loop()

    def connect_with_retry(self):
        while True:
            try:
                connection = BlockingConnection(self.rabbit_params)
                if connection.is_open:
                    return connection
            except Exception as e:
                self.logger.warning("RabbitMQ connection failed. Retrying in 5 seconds...", exc_info=True,
                                    method="connect_with_retry")
                sleep(5)

    def listen_for_commands(self):
        while True:
            try:
                connection = self.connect_with_retry()
                channel = connection.channel()

                channel.queue_declare(queue="simulation-commands", durable=True)

                def callback(ch, method, properties, body):
                    cmd = loads(body)
                    self.process_command(cmd)

                channel.basic_consume(queue="simulation-commands", on_message_callback=callback, auto_ack=True)
                channel.start_consuming()
            except Exception as e:
                self.logger.error("Consumer loop crashed", exc_info=True, method="listen_for_commands")
                sleep(5)

    def process_command(self, cmd_json):
        with self.lock:
            try:
                action = cmd_json.get("action")

                if action == "UPDATE_SIMULATION_CONFIG":
                    target_config = cmd_json["target_config"]

                    self.is_paused = target_config["is_paused"]
                    self.simulation_speed = target_config["simulation_speed"]
                    self.simulation_step = target_config["simulation_step"]
                    self.room_temperature_noise_sigma = target_config["room_temperature_noise_sigma"]

                elif action == "UPDATE_HVAC_CONFIG":
                    self.simulation.hvac.set_unit_config()

                elif action == "UPDATE_APARTMENT_CONFIG":
                    self.simulation.hvac.set_temperatures_config()

                elif action == "RESET":
                    self.reset_simulation_logic()

                self.wake_event.set()
            except Exception as e:
                self.logger.error("Failed to parse and process command", exc_info=True, payload=cmd_json,
                                  method="process_command")

    def reset_simulation_logic(self):
        self.simulation = DistrictSimulation("config/weather_history.csv")

        self.run_id = int(time())
        self.is_paused = True
        self.simulation_speed = 30
        self.simulation_step = 300
        self.room_temperature_noise_sigma = 0.5

    def run_physics_loop(self):
        pub_conn = None
        telemetry_channel = None
        config_channel = None

        while True:
            self.wake_event.clear()

            with self.lock:
                paused = self.is_paused
                speed = self.simulation_speed
                step = self.simulation_step
                room_temperature_noise_sigma = self.room_temperature_noise_sigma

            try:
                if pub_conn is None or pub_conn.is_closed:
                    pub_conn = self.connect_with_retry()

                    telemetry_channel = pub_conn.channel()
                    telemetry_channel.exchange_declare(
                        exchange="simulation-telemetry.exchange",
                        exchange_type="fanout",
                        durable=True
                    )

                    config_channel = pub_conn.channel()
                    config_channel.queue_declare(
                        queue="simulation-status",
                        durable=False,
                        arguments={"x-message-ttl": 1000}
                    )

                pub_conn.process_data_events(time_limit=0)

                self.broadcast_config(config_channel)

                if paused:
                    self.wake_event.wait(1.0)
                    continue

                start_time = time()

                simulation_result = self.simulation.run_step(step, room_temperature_noise_sigma)
                simulation_result = {"run_id": self.run_id, **simulation_result}

                telemetry_channel.basic_publish(
                    exchange="simulation-telemetry.exchange",
                    routing_key="",
                    body=dumps(simulation_result),
                    properties=BasicProperties(
                        content_type="application/json",
                        delivery_mode=DeliveryMode.Persistent
                    )
                )

                target_sleep = step / speed
                compute_time = time() - start_time
                real_sleep = max(0.0, target_sleep - compute_time)

                self.wake_event.wait(real_sleep)
            except Exception as e:
                self.logger.error("Failed to publish telemetry to RabbitMQ", exc_info=True, method="run_physics_loop")

                if pub_conn and pub_conn.is_open:
                    pub_conn.close()

                pub_conn = None

                self.wake_event.wait(1.0)

    def broadcast_config(self, channel):
        config_payload = {
            "config": {
                "is_paused": self.is_paused,
                "simulation_speed": self.simulation_speed,
                "simulation_step": self.simulation_step,
                "room_temperature_noise_sigma": self.room_temperature_noise_sigma}
        }

        channel.basic_publish(
            exchange="",
            routing_key="simulation-status",
            body=dumps(config_payload),
            properties=BasicProperties(content_type="application/json")
        )


if __name__ == "__main__":
    load_dotenv()
    service = SimulationService()
    service.start()
