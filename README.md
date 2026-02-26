# District Digital Twin (during development)

![Build Status](https://img.shields.io/badge/Build-Passing-brightgreen)
![Tech Stack](https://img.shields.io/badge/Stack-%20.NET%20|%20Python-blue)

A scalable Thermodynamic Digital Twin for residential districts, leveraging Model Predictive Control (MPC) and high-fidelity physics simulations to optimize energy efficiency.

## 💡 Concept

**🏙️ ResiFlow: Community Energy Management System (CEMS)**
An advanced IoT/SaaS platform for managing energy and comfort in multi-family residential buildings. The system uses Digital Twin technology to simulate building physics and predictive algorithms to optimize heating costs and balance power grid connections.

## 🎯 Main Goal

Creating a scalable backend system that manages a "virtual residential block" (and eventually an entire district), solving real problems of modern energy management:

- **Dynamic Load Management (DLM):** Preventing grid failures when charging electric vehicles (EVs).
- **Hybrid Optimization:** Automatic selection of the heat source (Gas vs. Heat Pump) depending on weather and electricity prices.
- **Fair Billing:** Precise billing for residents based on virtual heat meters.

## 🏗️ Infrastructure

- **Heat Source:** Hybrid heat substation (Gas Boiler + Heat Pump + Buffer Tank).
- **Power Source:** Rooftop PV installation + Grid connection (with power limit).
- **Load:** Household appliances/living loads + EV chargers in the garage.

## 🚀 Key Algorithms (Challenges)

**A. "Load Shedding" Algorithm (Garage)**

- **Problem:** The grid connection is 40kW. 6 apartments consume 20kW. Two Teslas want 11kW each (total 42kW). The breaker will trip.
- **Solution:** The backend detects increased consumption in apartments and dynamically throttles EV chargers (e.g., down to 2kW), opening them back up at night.

**B. "Economic Broker" Algorithm (Boiler Room)**

- **Problem:** What source should be used for heating?
- **Solution:**
  - _Scenario 1:_ The sun is shining (free PV electricity) -> Heat with the Heat Pump at maximum capacity (store heat in the buffer).
  - _Scenario 2:_ Night, freezing -15°C (Heat Pump has low COP, electricity is paid) -> Turn off the Heat Pump, start the Gas Boiler.

**C. Multi-tenancy & Security**

- **Data Isolation:** Neighbor A cannot see Neighbor B's bills.
- **RBAC (Role-Based Access Control):** Only the Property Manager can change the main boiler's heating curve.

## ⚙️ Architecture & Components

**Engine (Python)**

- Simulation microservice and Digital Twin that share the core engine code. The difference is that the simulation adds noise to simulated physical phenomena (to add realism), while the Digital Twin adds noise to the weather (simulating an imperfect weather forecast).
- Building thermodynamics simulation down to the individual room level. Includes heat transfer through floors, ceilings, roofs, external walls, windows, internal walls, wind impact (angle and speed), and solar radiation (angle and radiation).
- District definition via a `.yml` file.

**RabbitMQ**

- Bidirectional communication layer between the simulation and the API.
- Utilizes exchanges and queues.

**API (.NET)**

- Receiving and caching simulation results.
- Communication broker between the website and the engine.

**Telemetry Service (.NET)**

- Saving telemetry data to TimescaleDB.

**Website (.NET)**

- Control panel for the simulation.

## 📊 Monitoring

- Custom logging across all services.
- Custom Grafana dashboards made with SRE paradighms.
- Data retention and db backups

## 📝 To-Do (Next Tasks)

- grafana
- notifications
- container limits and reservations
- k8s with 2 clusters
- controller
- tenant panels
