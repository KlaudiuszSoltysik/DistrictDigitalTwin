from json import dumps, loads
from os import getenv
from threading import Lock, Thread
from time import sleep, time
from dotenv import load_dotenv

from pika import BlockingConnection, URLParameters, exceptions, DeliveryMode, BasicProperties

from DistrictSimulation import DistrictSimulation


class SimulationService:
    def __init__(self):
        amqp_url = getenv('RABBITMQ_CONNECTION_STRING')

        self.run_id = int(time())

        self.rabbit_params = URLParameters(amqp_url)

        self.simulation = DistrictSimulation("config/district_config.yaml", "config/weather_history.csv")

        # TODO: lock autostart
        self.lock = Lock()
        self.is_started = True
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
                time.sleep(5)

    def _listen_for_commands(self):
        while True:
            try:
                connection = self._connect_with_retry()
                channel = connection.channel()

                channel.queue_declare(queue='district.commands', durable=True)

                def callback(ch, method, properties, body):
                    try:
                        cmd = loads(body)
                        self._process_command(cmd)
                    except:
                        pass

                channel.basic_consume(queue='district.commands', on_message_callback=callback, auto_ack=True)
                channel.start_consuming()

            except:
                time.sleep(5)

    def _process_command(self, cmd):
        with self.lock:
            msg_type = cmd.get("type")
            payload = cmd.get("payload", {})

            if msg_type == "SYSTEM":
                action = payload.get("action")

                if action == "START":
                    self.is_started = True

                elif action == "STOP":
                    self.is_started = False

                elif action == "UPDATE_CONFIG":
                    if "simulation_speed" in payload:
                        self.simulation_speed = float(payload["simulation_speed"])
                        print(f"Simulation speed changed to: x{self.simulation_speed}")
                    if "simulation_step" in payload:
                        self.simulation_step = int(payload["simulation_step"])
                        print(f"Simulation step changed to: {self.simulation_step}s")

    def _run_physics_loop(self):
        pub_connection = self._connect_with_retry()
        pub_channel = pub_connection.channel()
        pub_channel.exchange_declare(exchange='district.telemetry.exchange', exchange_type='fanout', durable=True)

        while True:
            with self.lock:
                started = self.is_started
                speed = self.simulation_speed
                step = self.simulation_step

            if not started:
                sleep(0.5)
                continue

            start_time = time()

            try:
                simulation_result = self.simulation.run_step(step)

                simulation_result = {"run_id": self.run_id, **simulation_result}

                pub_channel.basic_publish(
                    exchange='district.telemetry.exchange',
                    routing_key='',
                    body=dumps(simulation_result),
                    properties=BasicProperties(
                        content_type='application/json',
                        delivery_mode=DeliveryMode.Persistent
                    )
                )

                print(f"Telemetry has been sent: {simulation_result}")

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
