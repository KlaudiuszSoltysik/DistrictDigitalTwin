import numpy as np

from yaml import safe_load


class DistrictModelParser:
    RHO_CP_AIR = 1200

    def __init__(self, yaml_path):
        with open(yaml_path, "r") as f:
            self.raw_data = safe_load(f)

        self.nodes = {}
        self.room_data = []
        self.external_connections = []

        self._build_node_index()
        self.N = len(self.nodes)

        self.G = np.zeros((self.N, self.N))
        self.G_ext_air = np.zeros(self.N)
        self.G_ext_ground = np.zeros(self.N)
        self.C = np.zeros(self.N)

        self.standards = {building["id"]: building["standards"] for building in self.raw_data["buildings"]}

    def _build_node_index(self):
        idx = 0

        for building in self.raw_data["buildings"]:
            for apartment in building["apartments"]:
                for room in apartment["rooms"]:
                    room_id = f"{building['id']}:{apartment['id']}:{room['id']}"

                    self.nodes[room_id] = idx
                    self.room_data.append({
                        "room": room,
                        "standards": building["standards"]
                    })

                    idx += 1

    def parse(self):
        for i, data in enumerate(self.room_data):
            room = data["room"]
            standards = data["standards"]

            c_air = room["volume"] * self.RHO_CP_AIR
            capacity_key = room["heat_capacity_per_m2"]
            capacity_value = standards[capacity_key]["heat_capacity_per_m2"]

            self.C[i] = c_air + (room["area"] * capacity_value)

        for building in self.raw_data["buildings"]:
            building_standards = building["standards"]

            if "internal_connections" in building:
                for connection in building["internal_connections"]:
                    self._apply_internal_connection(connection, building_standards, building["id"])

            if "external_connections" in building:
                for connection in building["external_connections"]:
                    self._apply_external_connection(connection, building_standards, building["id"])

        return (
            self.G,
            self.G_ext_air,
            self.G_ext_ground,
            self.C,
            self.N,
            self.external_connections,
            self.standards,
            self.nodes
        )

    def _apply_internal_connection(self, connection, standards, building_id):
        idx_a = self.nodes[f"{building_id}:{connection['from']}"]
        idx_b = self.nodes[f"{building_id}:{connection['to']}"]

        code = standards[connection["thermal_code"]]
        ua = connection["area"] * code["u_value"]

        self.G[idx_a, idx_b] += ua
        self.G[idx_b, idx_a] += ua

        wall_capacity = connection["area"] * code["heat_capacity_per_m2"]

        self.C[idx_a] += wall_capacity * 0.5
        self.C[idx_b] += wall_capacity * 0.5

    def _apply_external_connection(self, connection, standards, building_id):
        idx_a = self.nodes[f"{building_id}:{connection['from']}"]
        target = connection["to"]
        thermal_code = standards[connection["thermal_code"]]

        windows_to_solve = []
        windows_area_sum = 0

        for window in connection.get("windows", []):
            window_standard = standards[window["thermal_code"]]

            windows_area_sum += window["area"]

            windows_to_solve.append({
                "area": window["area"],
                "shgc": window_standard["shgc"]
            })

        if target != "ground":
            self.external_connections.append({
                "room_idx": idx_a,
                "azimuth": connection["azimuth"],
                "tilt": connection["tilt"],
                "area_gross": connection["area"],
                "windows": windows_to_solve,
                "volume": self.room_data[idx_a]["room"]["volume"],
                "ach_wind_coef": standards["ach_wind_coef"],
                "u_value": thermal_code["u_value"],
                "absorptance": thermal_code["absorptance"]
            })

        wall_net_area = connection["area"] - windows_area_sum
        ua_wall = wall_net_area * thermal_code["u_value"]

        ua_windows = sum(
            window["area"] * standards[window["thermal_code"]]["u_value"] for window in connection.get("windows", []))

        ua_total = ua_wall + ua_windows

        if target == "ground":
            self.G_ext_ground[idx_a] += ua_total
        else:
            self.G_ext_air[idx_a] += ua_total

        self.C[idx_a] += wall_net_area * thermal_code["heat_capacity_per_m2"]
