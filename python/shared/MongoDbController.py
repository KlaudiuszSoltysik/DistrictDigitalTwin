import os

from pymongo import MongoClient

from shared.logger_config import setup_logger


class MongoDbController:
    def __init__(self):
        self.logger = setup_logger("MongoDbController")

        try:
            self.client = MongoClient(os.getenv("MONGODB_CONNECTION_STRING", "mongodb://admin:admin@localhost:27017"))

            self.db = self.client["mongodb"]

        except Exception as e:
            self.logger.error("Failed to connect to MongoDB", exc_info=True, method="__init__")

    def get_device(self, device):
        return self.db["devices-config"].find_one({"device": device})

    def get_district(self):
        return self.client.db["district-config"].find_one({}, {"_id": 0})

    def update_device(self, device, config):
        self.db["devices-config"].update_one(
            {"device": device},
            {"$set": config},
            upsert=True
        )

    def close(self):
        self.client.close()
