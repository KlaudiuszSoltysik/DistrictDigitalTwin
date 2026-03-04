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
- Simulation of HVAC for entire district with custom profiles per every room (setted by tenant)
- District definition via a `.yml` file.

**Message Broker (RabbitMQ)**

- Bidirectional communication layer between the simulation and the API.
- Utilizes exchanges and routing queues for decoupling services.

**API (.NET)**

- Receiving and caching simulation results.
- Communication broker between the website and the engine.

**Telemetry Service (.NET)**

- Saving telemetry data to TimescaleDB for efficient time-series querying.

**Website (.NET)**

- Control panel and user interface for the simulation and HVAC

---

## 🏗️ Infrastructure & Deployment

- **Containerization:** All services (Python, .NET, Databases, Infrastructure) are fully containerized using Docker.
- **Orchestration:** Managed via Docker Compose with strict resource management (hard RAM/CPU limits and reservations) applied to containers to prevent starvation and ensure cluster stability.
- **Infrastructure as Code (IaC):** Core infrastructure components, including secure tunnels, are provisioned and managed using Terraform.

---

## 🔒 Networking & Security

- **Internal Networks:** Service-to-service communication is isolated within internal Docker networks (`ddt_net`), preventing unauthorized access and minimizing exposed ports.
- **Zero Trust Access:** External access to the cluster is secured via Cloudflare Tunnels (managed via Terraform), completely hiding public IP addresses and blocking direct inbound traffic.

---

## 📊 Observability & Monitoring Stack

- **Metrics Aggregation:** Prometheus scraping container metrics and internal service states.
- **Hardware Telemetry:** cAdvisor integration for real-time container resource monitoring (CPU, RAM, Network I/O, Disk I/O).
- **Log Aggregation:** Grafana Loki paired with Promtail for centralized, structured log management across the entire Docker cluster.
- **Instant Notifications:** Upon detecting anomalies or system failures, automated alerts are instantly dispatched to communication channel, enabling rapid incident response.
- **Broker Deep Monitoring:** Native RabbitMQ Prometheus plugin tracking real-time queue depths, message rates, and broker health.
- **Visualization:** Custom Grafana dashboards built with SRE paradigms (golden signals, error rate filtering, resource thresholds, dynamic unit scaling).
- **Data Retention:** Enforced automated data retention policies for both time-series metrics (Prometheus TSDB) and aggregated logs (Loki Compactor).

## 📝 To-Do (Next Tasks)

- container limits and reservations
- k8s with 2 clusters and dev/prod
- powerful cicd (terraform, fix postgres: makemigrations, migrate and create hypertables)
- controller
- tenant panels
