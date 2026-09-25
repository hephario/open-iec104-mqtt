/*
 * open-iec104-mqtt 
 * Copyright (C) 2026 Hephario GmbH
 *
 * This file is part of open-iec104-mqtt.
 * open-iec104-mqtt is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, version 3.
 */

using System;
using System.Text.Json.Serialization;

namespace Hephario.Iec104.Contracts
{
    /// <summary>
    /// Base class for all telemetry received from the IEC 104 bridge.
    /// The exact type is determined by the MQTT topic (e.g., iec104/bridge/telemetry/sp/1001)
    /// </summary>
    public abstract record TelemetryMessage
    {
        [JsonPropertyName("ioa")]
        public int Ioa { get; init; }

        [JsonPropertyName("quality")]
        public QualityInfo Quality { get; init; } = new();
    }

    public record QualityInfo
    {
        [JsonPropertyName("invalid")]
        public bool Invalid { get; init; }
        
        // Can be expanded to include: Substituted, Blocked, Overflow, etc.
    }

    /// <summary>
    /// Deserialization contract for Single Point (sp) telemetry
    /// </summary>
    public record SinglePointTelemetry : TelemetryMessage
    {
        [JsonPropertyName("value")]
        public bool Value { get; init; } 
    }

    /// <summary>
    /// Deserialization contract for Measured Values (mv)
    /// </summary>
    public record AnalogTelemetry : TelemetryMessage
    {
        [JsonPropertyName("value")]
        public float Value { get; init; } 
    }

    /// <summary>
    /// Commands (Sent from C# App -> Bridge -> IEC 104)
    /// </summary>
    public record SinglePointCommand
    {
        [JsonPropertyName("transaction_id")]
        public string? TransactionId { get; init; }

        [JsonPropertyName("execute")]
        public bool Execute { get; init; }
    }

    public record DoublePointCommand
    {
        [JsonPropertyName("transaction_id")]
        public string? TransactionId { get; init; }

        [JsonPropertyName("value")]
        public int Value { get; init; } // 0=Intermediate, 1=Off, 2=On, 3=Indeterminate
    }

    public record AnalogSetpointCommand
    {
        [JsonPropertyName("transaction_id")]
        public string? TransactionId { get; init; }

        [JsonPropertyName("value")]
        public float Value { get; init; } 
    }

    /// <summary>
    /// Command Status (Received from Bridge: ACT_CON / ACT_TERM)
    /// </summary>
    public record CommandStatusMessage
    {
        [JsonPropertyName("ioa")]
        public int Ioa { get; init; }

        [JsonPropertyName("transaction_id")]
        public string? TransactionId { get; init; }

        [JsonPropertyName("cot")]
        public string CauseOfTransmission { get; init; } = string.Empty; // e.g. "ACTIVATION_CON", "ACTIVATION_TERMINATION"

        [JsonPropertyName("isNegative")]
        public bool IsNegative { get; init; } // True if the command was rejected

        [JsonPropertyName("typeId")]
        public string TypeId { get; init; } = string.Empty; // e.g. "C_SC_NA_1"
    }
}

