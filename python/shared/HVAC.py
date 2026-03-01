import numpy as np

import numpy as np

class HVAC:
    def __init__(self, num_nodes, max_heating_powers, k_i=0.05, tau_hours=2.0):
        self.num_nodes = num_nodes
        self.max_powers = max_heating_powers

        self.setpoints = np.full(num_nodes, 21.0)

        self.k_p = self.max_powers / 2.0
        self.k_i = np.full(num_nodes, k_i)

        self.integral_error = np.zeros(num_nodes)

        self.tau = tau_hours * 3600.0
        self.current_q = np.zeros(num_nodes)

    def step(self, dt, T):
        error = self.setpoints - T
        error = np.clip(error, 0.0, None)

        p_term = error * self.k_p

        self.integral_error += error * dt

        max_integral = self.max_powers / (self.k_i + 1e-9)
        self.integral_error = np.clip(self.integral_error, 0.0, max_integral)

        i_term = self.integral_error * self.k_i

        q_target = p_term + i_term

        q_target = np.clip(q_target, 0.0, self.max_powers)

        alpha = dt / self.tau
        alpha = min(alpha, 1.0)
        self.current_q += alpha * (q_target - self.current_q)

        return self.current_q