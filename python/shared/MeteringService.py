import numpy as np


class MeteringService:
    def __init__(self, num_nodes, index_to_id):
        self.num_nodes = num_nodes
        self.index_to_id = index_to_id

        self.total_elec_import = 0.0
        self.total_elec_export = 0.0
        self.total_pv_yield = 0.0
        self.total_gas_import = 0.0

        self.admin_elec_cost = 0.0
        self.admin_gas_cost = 0.0

        self.admin_elec_revenue = 0.0
        self.total_tenant_revenue = 0.0

        self.room_heat_delivered = {self.index_to_id[i]: 0.0 for i in range(self.num_nodes)}
        self.room_cool_delivered = {self.index_to_id[i]: 0.0 for i in range(self.num_nodes)}
        self.room_vent_volume = {self.index_to_id[i]: 0.0 for i in range(self.num_nodes)}

        self.room_billing_cost = {self.index_to_id[i]: 0.0 for i in range(self.num_nodes)}

    def update_meters(self, dt, energy_costs, q_hvac, v_hvac):
        q_heating = np.maximum(0, q_hvac)
        q_cooling = np.maximum(0, -q_hvac)

        heat_elec = np.sum(q_heating) / energy_costs["cop_heating"] / 1000.0
        cool_elec = np.sum(q_cooling) / energy_costs["cop_cooling"] / 1000.0
        # converting ventilation m3/h to kWh with 1:1 proportions
        vent_elec = np.sum(v_hvac)

        total_elec = heat_elec + cool_elec + vent_elec

        grid_buy = np.maximum(0, total_elec - energy_costs["pv_farm_yield"])
        grid_sell = np.maximum(0, energy_costs["pv_farm_yield"] - total_elec)

        gas_buy = 0.0

        hours = dt / 3600.0

        self.total_pv_yield += energy_costs["pv_farm_yield"] * hours
        self.total_elec_import += grid_buy * hours
        self.total_elec_export += grid_sell * hours
        self.admin_elec_cost += (grid_buy * hours) * energy_costs["electricity_cost"]

        self.total_gas_import += gas_buy * hours
        self.admin_gas_cost += (gas_buy * hours) * energy_costs["gas_cost"]

        tenant_tariff = 0.35  # EUR za 1 kWh

        export_tariff = max(0.0, energy_costs["electricity_cost"])
        self.admin_elec_revenue += (grid_sell * hours) * export_tariff

        for i in range(self.num_nodes):
            room_id = self.index_to_id[i]
            power = q_hvac[i]
            vent = v_hvac[i]

            room_elec = 0.0

            if power > 0:
                self.room_heat_delivered[room_id] += (power / 1000.0) * hours
                room_elec += (power / energy_costs["cop_heating"]) / 1000.0
            elif power < 0:
                self.room_cool_delivered[room_id] += (abs(power) / 1000.0) * hours
                room_elec += (abs(power) / energy_costs["cop_cooling"]) / 1000.0

            self.room_vent_volume[room_id] += vent * dt
            room_elec += vent

            step_room_energy_kwh = room_elec * hours

            step_room_cost = step_room_energy_kwh * tenant_tariff

            self.room_billing_cost[room_id] += step_room_cost
            self.total_tenant_revenue += step_room_cost

    def get_meter_readings(self):
        cost_margin = (self.admin_elec_revenue + self.total_tenant_revenue) - (
                    self.admin_elec_cost + self.admin_gas_cost)

        return {
            "admin_meters": {
                "electricity_import": round(self.total_elec_import, 3),
                "electricity_export": round(self.total_elec_export, 3),
                "pv_farm_yield": round(self.total_pv_yield, 3),
                "gas_import": round(self.total_gas_import, 3),
                "elec_cost": round(self.admin_elec_cost, 2),
                "gas_cost": round(self.admin_gas_cost, 2),

                "admin_elec_revenue": round(self.admin_elec_revenue, 2),
                "tenant_billing_revenue": round(self.total_tenant_revenue, 2),
                "cost_margin": round(cost_margin, 2)
            },
            "tenant_meters": {
                "heating": {k: round(v, 3) for k, v in self.room_heat_delivered.items()},
                "cooling": {k: round(v, 3) for k, v in self.room_cool_delivered.items()},
                "ventilation": {k: round(v, 2) for k, v in self.room_vent_volume.items()},

                "billing_cost": {k: round(v, 2) for k, v in self.room_billing_cost.items()}
            }
        }
