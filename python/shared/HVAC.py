import numpy as np

from shared.MongoDbController import MongoDbController


class HVAC:
    def __init__(self, num_nodes, max_heating_powers):
        self.mongodb = MongoDbController()

        hvac_config = self.mongodb.get_device("hvac")

        self.num_nodes = num_nodes
        self.max_powers = max_heating_powers

        # TODO: change that
        self.setpoints = np.full(num_nodes, 21.0)

        self.integral_error = np.zeros(num_nodes)
        self.current_q = np.zeros(num_nodes)

        self.k_p = None
        self.k_i = None
        self.set_config({"p_band": hvac_config["p_band"], "t_i": hvac_config["t_i"]})

        # TODO: change that
        self.tau = 4.0 * 3600.0  # 1-8

    def set_config(self, config):
        p_band = config["p_band"]  # 1-4
        self.k_p = self.max_powers / p_band

        t_i = config["t_i"]  # 1-8
        self.k_i = self.k_p / t_i

    def step(self, dt, T):
        error = self.setpoints - T

        p_term = error * self.k_p

        self.integral_error += error * dt

        max_integral = self.max_powers / self.k_i
        self.integral_error = np.clip(self.integral_error, 0.0, max_integral)

        i_term = self.integral_error * self.k_i

        q_target = p_term + i_term

        q_target = np.clip(q_target, 0.0, self.max_powers)

        alpha = dt / self.tau
        alpha = min(alpha, 1.0)
        self.current_q += alpha * (q_target - self.current_q)

        return self.current_q
