#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExtractionWeight.Zone
{
    [Serializable]
    public sealed class ThreatPatrolRoute
    {
        [field: SerializeField] public string RouteId { get; private set; } = string.Empty;
        [field: SerializeField] public List<Vector3> Waypoints { get; private set; } = new();

        public ThreatPatrolRoute(string routeId, List<Vector3> waypoints)
        {
            RouteId = routeId;
            Waypoints = waypoints ?? new List<Vector3>();
        }

        public ThreatPatrolRoute()
        {
        }
    }
}
