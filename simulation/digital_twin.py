from json import dumps, loads
from os import getenv
from time import sleep

import numpy as np
import pandas as pd
from dotenv import load_dotenv
from pika import BlockingConnection, URLParameters, DeliveryMode, BasicProperties

from DistrictSimulation import DistrictSimulation


class DigitalTwinService:
    def __init__(self):
        amqp_url = getenv("RABBITMQ_CONNECTION_STRING")
        self.rabbit_params = URLParameters(amqp_url)

        self.simulation = DistrictSimulation("config/district_config.yaml", "config/weather_history.csv", True)

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
            except:
                sleep(5)

    def listen_for_commands(self):
        while True:
            try:
                connection = self.connect_with_retry()
                channel = connection.channel()

                channel.queue_declare(queue="digital_twin_commands", durable=True)

                def callback(ch, method, properties, body):
                    cmd = loads(body)
                    self.process_command(cmd)

                channel.basic_consume(
                    queue="digital_twin_commands",
                    on_message_callback=callback,
                    auto_ack=True
                )
                channel.start_consuming()

            except:
                sleep(5)

    def process_command(self, cmd_json):
        start_ts = pd.Timestamp(cmd_json["start_timestamp"])
        end_ts = start_ts.normalize() + pd.Timedelta(days=2)

        self.simulation.current_time = start_ts
        self.simulation.thermal_solver.T = np.array(cmd_json["T"])

        self.run_physics_loop(end_ts)

    def run_physics_loop(self, end_timestamp):
        simulation_result = []

        while self.simulation.current_time < end_timestamp:
            step_data = self.simulation.run_step(self.simulation_step)
            simulation_result.append(step_data)

        try:
            pub_conn = self.connect_with_retry()

            telemetry_channel = pub_conn.channel()

            telemetry_channel.exchange_declare(
                exchange="digital_twin_telemetry.exchange",
                exchange_type="fanout",
                durable=True
            )

            telemetry_channel.basic_publish(
                exchange="digital_twin_telemetry.exchange",
                routing_key="",
                body=dumps(simulation_result),
                properties=BasicProperties(
                    content_type="application/json",
                    delivery_mode=DeliveryMode.Persistent
                )
            )

            pub_conn.close()
        except:
            pass


if __name__ == "__main__":
    load_dotenv()
    service = DigitalTwinService()
    service.start()
