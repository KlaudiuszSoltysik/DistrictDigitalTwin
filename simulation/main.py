from json import dumps, loads
from os import getenv
from threading import Lock, Thread
from time import sleep, time

from dotenv import load_dotenv
from pika import BlockingConnection, URLParameters, exceptions, DeliveryMode, BasicProperties

from DistrictSimulation import DistrictSimulation


class SimulationService:
    def __init__(self):
        amqp_url = getenv("RABBITMQ_CONNECTION_STRING")

        self.run_id = int(time())

        self.rabbit_params = URLParameters(amqp_url)

        self.simulation = DistrictSimulation("config/district_config.yaml", "config/weather_history.csv")

        self.lock = Lock()
        self.is_paused = True
        self.simulation_speed = 30
        self.simulation_step = 300

    def start(self):
        listener_thread = Thread(target=self._listen_for_commands, daemon=True)
        listener_thread.start()

        self._run_physics_loop()

    def _connect_with_retry(self):
        while True:
            try:
                connection = BlockingConnection(self.rabbit_params)
                if connection.is_open:
                    return connection
            except (exceptions.AMQPConnectionError, OSError):
                sleep(5)

    def _listen_for_commands(self):
        while True:
            try:
                connection = self._connect_with_retry()
                channel = connection.channel()

                channel.queue_declare(queue="commands", durable=True)

                def callback(ch, method, properties, body):
                    try:
                        cmd = loads(body)
                        self._process_command(cmd)
                    except:
                        pass

                channel.basic_consume(queue="commands", on_message_callback=callback, auto_ack=True)
                channel.start_consuming()

            except:
                sleep(5)

    def _process_command(self, cmd_json):
        with self.lock:
            action = cmd_json.get("action")
            target_config = cmd_json.get("target_config", {})

            if action == "UPDATE_CONFIG":
                if "is_paused" in target_config:
                    self.is_paused = target_config["is_paused"]
                if "simulation_speed" in target_config:
                    self.simulation_speed = target_config["simulation_speed"]
                if "simulation_step" in target_config:
                    self.simulation_step = target_config["simulation_step"]

            # elif action == "RESET":
            #     self._reset_simulation_logic()

    def _broadcast_config(self, channel):
        config_payload = {"config": {
            "is_paused": self.is_paused,
            "simulation_speed": self.simulation_speed,
            "simulation_step": self.simulation_step
        }}
        channel.basic_publish(
            exchange="",
            routing_key="status",
            body=dumps(config_payload),
            properties=BasicProperties(content_type="application/json")
        )

    def _run_physics_loop(self):
        pub_connection = self._connect_with_retry()
        telemetry_channel = pub_connection.channel()
        telemetry_channel.exchange_declare(exchange="telemetry.exchange", exchange_type="fanout", durable=True)

        config_channel = pub_connection.channel()
        config_channel.queue_declare(queue="status", durable=True)

        while True:
            with self.lock:
                paused = self.is_paused
                speed = self.simulation_speed
                step = self.simulation_step

            self._broadcast_config(config_channel)

            if paused:
                sleep(1)
                continue

            start_time = time()

            try:
                simulation_result = self.simulation.run_step(step)

                simulation_result = {"run_id": self.run_id, **simulation_result}

                telemetry_channel.basic_publish(
                    exchange="telemetry.exchange",
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

                sleep(real_sleep)

            except:
                sleep(1)


if __name__ == "__main__":
    load_dotenv()
    service = SimulationService()
    service.start()
