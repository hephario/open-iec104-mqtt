#!/bin/bash

# Install mosquitto clients and bash
apk add --no-cache mosquitto-clients bash

echo "Starting IEC 104 simulation (CP8050 RTU profile)..."

# Initial values
TEMPERATURE=22.5
BREAKER_STATUS="false"

while true; do
  # 1. Simulate RTU reading a temperature sensor and sending it to the 'Server' bridge (which forwards via IEC104)
  # In Server mode, sending to /telemetry_in updates the server's internal model.
  # The bridge translates this and the Client bridge receives it.
  TEMPERATURE=$(awk "BEGIN {print $TEMPERATURE + 0.1}")
  echo "Simulated RTU Sensor reading: $TEMPERATURE C (IOA: 1001)"
  
  mosquitto_pub -h mqtt-broker -t "iec104/rtu_sim/telemetry_in/mv/1001" -m "{\"value\": $TEMPERATURE, \"quality\": {\"invalid\": false}}"
  
  # 2. Simulate reading an IO actuator (Breaker Status - IOA: 2001)
  mosquitto_pub -h mqtt-broker -t "iec104/rtu_sim/telemetry_in/sp/2001" -m "{\"value\": $BREAKER_STATUS, \"quality\": {\"invalid\": false}}"
  
  # 3. Simulate SCADA Client randomly sending a command to flip the breaker
  RAND=$((1 + RANDOM % 10))
  if [ $RAND -gt 8 ]; then
    if [ "$BREAKER_STATUS" = "false" ]; then
        BREAKER_STATUS="true"
    else
        BREAKER_STATUS="false"
    fi
    echo "SCADA Operator toggling breaker to: $BREAKER_STATUS (IOA: 2001)"
    mosquitto_pub -h mqtt-broker -t "iec104/scada_client/command/cmd/2001" -m "{\"execute\": $BREAKER_STATUS}"
  fi

  sleep 2
done
