﻿using System.Collections.Generic;
using Kohctpyktop.Models.Field;

namespace Kohctpyktop.Models.Topology
{
    /// <summary>
    /// Handles groups of gates directly following each other.
    /// A singular gate is counted as a group of one. 
    /// </summary>
    public class SchemeGate
    {
        /// <summary>
        /// Arrays of one or two inputs OR'ed for each consecutive gate
        /// </summary>
        public List<SchemeNode[]> GateInputs { get; set; } = new List<SchemeNode[]>();
        /// <summary>
        /// The exactly 2 nodes connected or disconnected by the gate
        /// </summary>
        public List<SchemeNode> GatePowerNodes { get; set; } = new List<SchemeNode>();
        /// <summary>
        /// true if the gate is open if and only if the input is low (PNP)
        /// otherwise the gate is open if and only if the input is high (NPN)
        /// </summary>
        public bool IsInversionGate { get; set; }
        /// <summary>
        /// The current state of the gate
        /// </summary>
        public bool IsOpen { get; set; }
        /// <summary>
        /// The state of the gate during the previous simulation step
        /// </summary>
        public bool WasOpen { get; set; }
        public List<ILayerCell> GateCells { get; set; } = new List<ILayerCell>();
    }
}