import numpy as np

class HVAC:
    def __init__(self, num_nodes, base_setpoint=21.0, max_power_w=2000.0, k_p=500.0, time_constant_hours=2.0):
        self.num_nodes = num_nodes

        self.setpoints = np.full(num_nodes, base_setpoint)
        self.max_powers = np.full(num_nodes, max_power_w)
        self.k_p = k_p

        self.tau = time_constant_hours * 3600.0

        self.current_q = np.zeros(num_nodes)

    def step(self, dt, T):
        error = self.setpoints - T
        error = np.clip(error, 0.0, None)

        q_target = error * self.k_p
        q_target = np.clip(q_target, 0.0, self.max_powers)

        alpha = dt / self.tau
        alpha = min(alpha, 1.0)

        self.current_q += alpha * (q_target - self.current_q)

        return self.current_q