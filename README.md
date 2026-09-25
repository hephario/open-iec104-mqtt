# open-iec104 MQTT Bridge (C# / .NET 8)

[![Docker Image CI/CD](https://github.com/hephario/open-iec104-mqtt/actions/workflows/docker-publish.yml/badge.svg)](https://github.com/hephario/open-iec104-mqtt/actions/workflows/docker-publish.yml)
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](https://www.gnu.org/licenses/gpl-3.0)
[![Docker Pulls](https://img.shields.io/docker/pulls/hephario/open-iec104-mqtt)](https://hub.docker.com/r/hephario/open-iec104-mqtt)

An enterprise-grade, isolated, dual-sided (Client or Server) IEC 60870-5-104 to MQTT bridge.
Designed for 24/7 long-term industrial stability on edge devices using a minimal .NET 8 container.

## Architecture & Configuration Flexibility

This bridge is purposefully designed to be **dumb and generic**. It blindly translates IEC 104 Information Object Addresses (IOAs) directly into MQTT topics (e.g., `iec104/bridge_id/sp/1001` for IOA 1001). 

**Why?** 
Instead of configuring massive tag mappings inside this bridge, your main commercial application simply consumes all MQTT messages from the bridge and handles the filtering/mapping internally. This gives your application total configuration flexibility and completely decouples the GPL-licensed IEC 104 boundary from your intellectual property.

## Licensing
This component is designed to link against the open-source GPLv3 version of `lib60870`. Therefore, this bridge itself is licensed under **GPLv3**. Running this as an isolated Docker container communicating via MQTT ensures your commercial app does not become a derivative work.

## Configuration
Configure entirely via environment variables (see `docker-compose.yml`):
- `BRIDGE_ID`: Unique ID for MQTT topics
- `IEC104_MODE`: `client` or `server`
- `IEC104_IP`: IP to connect or bind to
- `MQTT_HOST`: Broker IP
- `MQTT_QOS`: 0, 1, or 2 (supports MQTTv5)
- `MQTT_RETAIN`: true or false
- `REQUIRE_TRANSACTION_ID`: `true` or `false` (If true, rejects commands without a `transaction_id`)

## Examples

### 1. Telemetry (IEC 104 -> MQTT)
When the bridge receives an ASDU (e.g., Single Point Information) from an IEC 104 outstation, it translates and publishes it to MQTT.

**Topic:** `iec104/substation_bridge_01/telemetry/sp/1001`
**Payload:**
```json
{
  "ioa": 1001,
  "value": true,
  "quality": {
    "invalid": false
  }
}
```

### 2. Command (MQTT -> IEC 104)
To send a control command (e.g., Single Command) down to the IEC 104 outstation, publish to the command topic:

**Topic:** `iec104/substation_bridge_01/command/cmd/1001`
**Payload:**
```json
{
  "transaction_id": "req-12345",
  "execute": true
}
```

### 3. Analog Setpoint (Float) (MQTT -> IEC 104)
To send a 32-bit float analog setpoint (Short Floating Point) to the outstation:

**Topic:** `iec104/substation_bridge_01/command/setpoint_float/2001`
**Payload:**
```json
{
  "value": 45.75
}
```

## Running
```bash
docker-compose up -d --build
```

