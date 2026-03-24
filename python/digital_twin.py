from json import dumps, loads
from os import getenv
from time import sleep

import numpy as np
import pandas as pd
from dotenv import load_dotenv
from pika import BlockingConnection, URLParameters, DeliveryMode, BasicProperties

from shared.DistrictSimulation import DistrictSimulation
from shared.logger_config import setup_logger


class DigitalTwinService:
    def __init__(self):
        self.logger = setup_logger("digital_twin")

        amqp_url = getenv("RABBITMQ_CONNECTION_STRING")
        self.rabbit_params = URLParameters(amqp_url)

        self.simulation = DistrictSimulation("config/weather_history.csv", True)

        self.run_id = 0
        self.simulation_step = 300
        self.pub_connection = None
        self.telemetry_channel = None

    def start(self):
        self.listen_for_commands()

    def connect_with_retry(self):
        while True:
            try:
                connection = BlockingConnection(self.rabbit_params)
                if connection.is_open:
                    return connection
            except Exception:
                self.logger.warning("RabbitMQ connection failed. Retrying in 5 seconds...", exc_info=True,
                                    method="connect_with_retry")
                sleep(5)

    def listen_for_commands(self):
        while True:
            try:
                connection = self.connect_with_retry()
                channel = connection.channel()

                channel.queue_declare(queue="digital-twin-commands", durable=True)

                def callback(ch, method, properties, body):
                    cmd = loads(body)
                    self.process_command(cmd)

                channel.basic_consume(
                    queue="digital-twin-commands",
                    on_message_callback=callback,
                    auto_ack=True
                )

                channel.start_consuming()
            except Exception:
                self.logger.error("Consumer loop crashed", exc_info=True, method="listen_for_commands")
                sleep(5)

    def process_command(self, cmd_json):
        try:
            start_ts = pd.Timestamp(cmd_json["start_timestamp"])
            end_ts = start_ts + pd.Timedelta(hours=24)

            self.simulation.current_time = start_ts
            self.simulation.thermal_solver.T = np.array(list(cmd_json["t"].values()), dtype=float)
            self.simulation.co2_solver.co2 = np.array(list(cmd_json["co2"].values()), dtype=float)

            self.simulation.co2_solver.set_on_hours()

            self.simulation.hvac.set_temperatures_config()
            self.simulation.hvac.cached_t_plan = None
            self.simulation.hvac.cached_v_plan = None
            self.simulation.hvac.plan_step_index = 0

            self.run_physics_loop(end_ts)
        except Exception as e:
            import traceback
            traceback.print_exc()
            print("Error ", e)

            self.logger.error("Failed to parse and process command", exc_info=True, payload=cmd_json,
                              method="process_command")

    def run_physics_loop(self, end_timestamp):
        try:
            telemetry = []
            while self.simulation.current_time < end_timestamp:
                step_data = self.simulation.run_step(self.simulation_step)
                telemetry.append({"run_id": self.run_id, **step_data})

            pub_conn = self.connect_with_retry()

            telemetry_channel = pub_conn.channel()

            telemetry_channel.exchange_declare(
                exchange="digital-twin-telemetry.exchange",
                exchange_type="fanout",
                durable=True
            )

            telemetry_channel.basic_publish(
                exchange="digital-twin-telemetry.exchange",
                routing_key="",
                body=dumps(telemetry),
                properties=BasicProperties(
                    content_type="application/json",
                    delivery_mode=DeliveryMode.Persistent
                )
            )

            pub_conn.close()
        except Exception as e:
            import traceback
            traceback.print_exc()
            print("Error ", e)

            self.logger.error("Failed to publish telemetry to RabbitMQ", exc_info=True, method="run_physics_loop")


if __name__ == "__main__":
    load_dotenv()
    service = DigitalTwinService()
    service.start()
