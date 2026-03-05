import yaml

from shared.MongoDbController import MongoDbController

mongodb = MongoDbController()
collection = mongodb.db["district-config"]

with open("../config/district.yml", "r", encoding="utf-8") as file:
    config_data = yaml.safe_load(file)

collection.insert_one(config_data)
