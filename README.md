# District Digital Twin (during development)

![Build Status](https://img.shields.io/badge/Build-Passing-brightgreen)
![Tech Stack](https://img.shields.io/badge/Stack-%20.NET%20|%20Python-blue)

A scalable Thermodynamic Digital Twin for residential districts, leveraging Model Predictive Control (MPC) and high-fidelity physics simulations to optimize energy efficiency.

## 💡 Concept

An advanced IoT/SaaS platform for managing energy and comfort in multi-family residential buildings. The system uses Digital Twin technology to simulate building physics and predictive algorithms to optimize heating costs and balance power grid connections.

## 🎯 Main Goal

Creating a scalable backend system that manages a virtual residential district, solving real problems of modern energy management:

- **Dynamic Load Management (DLM):** Preventing grid failures when charging electric vehicles (EVs).
- **Hybrid Optimization:** Automatic selection of the heat source (Gas vs. Heat Pump) depending on weather and electricity prices.
- **Fair Billing:** Precise billing for residents based on virtual heat meters.

## 🏗️ Infrastructure

- **Heat Source:** Hybrid heat substation (Gas Boiler + Heat Pump + Buffer Tank).
- **Power Source:** Rooftop PV installation + Grid connection (with power limit).
- **Load:** Household appliances/living loads + EV chargers in the garage.

## ⚙️ Architecture & Components

**Engine (Python)**

- Simulation microservice and Digital Twin that share the core engine code. The difference is that the simulation adds noise to simulated physical phenomena (to add realism), while the Digital Twin adds noise to the weather (simulating an imperfect weather forecast).
- Building thermodynamics and CO2 mass-flow simulation down to the individual room level. Includes heat transfer through floors, ceilings, roofs, external walls, windows, internal walls, wind impact (angle and speed), solar radiation, natural air infiltration, and human CO2 generation.
- Simulation of HVAC for entire district with custom profiles per every room (setted by tenant).
- Advanced Model Predictive Control (MPC) solver (L-BFGS-B) managing HVAC for the entire district. Capabilities include:
  - MIMO Architecture (Multiple-Input Multiple-Output): Simultaneous algorithmic control over thermal power ($Q_{hvac}$) and mechanical ventilation volume ($V_{vent}$). The solver dynamically balances the trade-off between flushing out CO2 and the thermal cost of conditioning outside air, factoring in Heat Recovery Ventilation (HRV) efficiency.
  - Feature Scaling & Numerical Stability: The internal optimization space is fully normalized (0-100%) to stabilize the Hessian matrix, allowing the L-BFGS-B solver to smoothly navigate the gradient between completely different physical domains (Watts vs. $m^3/s$) without numerical explosions.
  - Bi-directional HVAC: Seamless algorithmic switching between floor heating and floor cooling with dew-point safety constraints.
  - Dynamic Deadbands & 24h Scheduling: Tenants set custom temperature ranges, CO2 limits, and hourly ON/OFF schedules.
  - Pre-heating / Pre-cooling: The MPC algorithm anticipates scheduled status changes and dynamically shifts thermal loads to prepare rooms in advance using minimal energy.
  - Hardware Slew-Rate Limits: Soft and hard constraints preventing damaging power spikes and rapid damper oscillations in physical hardware.
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

- Control panel and user interface divided into admin and tenant sections.
  - Admin: controll the simulation, descriptive charts
  - Tenant: Control HVAC settings for each room

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
- add connection status (?)
- divide into tenant and admin systems
- billing system
- fix hvac spikes
- grafana
