# Docker deployment

Make sure that the [prerequisites](dev-run-prerequisites.md) have been met before continuing

# Instances

* Integration
  - Kafka - 1 Zookeeper, 2 brokers
* Data storage
  - YugabyteDB - 1 master, 1 tserver
  - Cassandra - 2 nodes
  - Redis - 3 masters
* Observability
  - Telemetry - 1 OTel collector
  - Tracing - 1 Jaeger all-in-one
  - Metrics - 1 Prometheus, 1 cAdvisor, 1 Grafana
  - Logging - 1 ElasticSearch, 1 Kibana
* CecoChat
  - BFF branch - 1 BFF, 1 User, 1 Chats
  - Messaging branch - 2 Messaging, 1 IDGen

# Manual setup

**Before** running most of the containers `docker volume`s need to be created for the components below, using the related scripts in the respective folder:

* Integration:
  - Kafka
* Data storage:
  - YugabyteDB
  - Cassandra
  - Redis
* Observability
  - ElasticSearch

**After** running some of the containers they need to be prepared additionally using `docker exec -it <container-name> bash` with the content of the related scripts in the respective folder. This is a one time setup which persists data in the volume. If the volume is recreated it needs to be repeated.

* Data storage:
  - Redis initial dynamic configuration - unfortunately, due to how Redis works, the contents of the script needs to be executed in each of the 3 instances of the cluster
* Observability
  - Grafana dashboards import

# Run

The convenience of `docker compose` makes it possible to run exactly what is needed, for example:

* Working on the Messaging service:
  - Run the IDGen service from the IDE
  - `docker compose -f redis.yml up -d`
  - `docker compose -f kafka.yml up -d`
* Working on Chats service:
  - `docker compose -f redis.yml up -d`
  - `docker compose -f kafka.yml up -d`
  - `docker compose -f cassandra.yml up -d`
  - Run the IDGen and Messaging services from the IDE to input messages into Kafka
  - Run the BFF service from the IDE to make queries
* Working on observability:
  - `docker compose -f telemetry.yml up -d`
  - `docker compose -f tracing.yml up -d`
  - `docker compose -f metrics.yml up -d`
  - `docker compose -f logging.yml up -d`
* Start all CecoChat containers in docker:
  - `docker compose -f cecochat-messaging.yml up -d`
  - `docker compose -f cecochat-bff.yml up -d`
* In order to stop containers or destroy them:
  - `docker compose -f <some.yml> stop`
  - `docker compose -f <some.yml> down`
