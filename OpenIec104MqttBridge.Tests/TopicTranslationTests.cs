/*
 * open-iec104-mqtt 
 * Copyright (C) 2026 Hephario GmbH
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using Xunit;

namespace OpenIec104MqttBridge.Tests
{
    public class TopicTranslationTests
    {
        // In a real scenario with lib60870.NET linked, we would mock the connection.
        // Here we test the theoretical string translation logic we'll extract to a helper.

        [Fact]
        public void Test_CommandTopic_ParsesCorrectly()
        {
            // Arrange
            string topic = "iec104/bridge1/command/cmd/2005";
            
            // Act
            var parts = topic.Split('/');
            
            // Assert
            Assert.Equal(5, parts.Length);
            Assert.Equal("bridge1", parts[1]);
            Assert.Equal("command", parts[2]);
            Assert.Equal("cmd", parts[3]);
            Assert.Equal("2005", parts[4]);
            
            int ioa = int.Parse(parts[4]);
            Assert.Equal(2005, ioa);
        }

        [Fact]
        public void Test_TelemetryTopic_GeneratesCorrectly()
        {
            // Arrange
            string bridgeId = "substation_x";
            int ioa = 1001;
            string type = "mv"; // measured value
            
            // Act
            string generatedTopic = $"iec104/{bridgeId}/telemetry/{type}/{ioa}";
            
            // Assert
            Assert.Equal("iec104/substation_x/telemetry/mv/1001", generatedTopic);
        }
    }
}

