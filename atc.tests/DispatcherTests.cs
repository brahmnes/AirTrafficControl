﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AirTrafficControl.Interfaces;

namespace AirTrafficControl.Tests
{
    [TestClass]
    public class DispatcherTests
    {
        [TestMethod]
        public void SingleRoutePath()
        {
            Airport ksea = Universe.Current.Airports.Where(a => a.Name == "KSEA").First();
            Airport kpdx = Universe.Current.Airports.Where(a => a.Name == "KPDX").First();

            var path = Dispatcher.ComputeFlightPath(ksea, kpdx);
            VerifyPath(path, new[] { "KSEA", "MALAY", "KPDX" });
        }

        [TestMethod]
        public void MultiRoutePath()
        {
            Airport kbzn = Universe.Current.Airports.Where(a => a.Name == "KBZN").First();
            Airport kpdx = Universe.Current.Airports.Where(a => a.Name == "KPDX").First();

            var path = Dispatcher.ComputeFlightPath(kpdx, kbzn);
            VerifyPath(path, new [] { "kpdx", "ykm", "mwh", "kgeg", "mso", "hln", "kbzn"});
        }

        [TestMethod]
        public void PathWithFixOriginatingRoutes()
        {
            Airport kboi = Universe.Current.Airports.Where(a => a.Name == "KBOI").First();
            Airport kbzn = Universe.Current.Airports.Where(a => a.Name == "KBZN").First();

            var path = Dispatcher.ComputeFlightPath(kboi, kbzn);
            VerifyPath(path, new[] { "kboi", "dnj", "hia", "kbzn" });
        }

        [TestMethod]
        public void BothWaysSamePathLength()
        {
            Airport kgeg = Universe.Current.Airports.Where(a => a.Name == "KGEG").First();
            Airport kmfr = Universe.Current.Airports.Where(a => a.Name == "KMFR").First();

            var path = Dispatcher.ComputeFlightPath(kgeg, kmfr);
            var reversePath = Dispatcher.ComputeFlightPath(kmfr, kgeg);
            Assert.AreEqual(path.Count, reversePath.Count);
        }

        private void VerifyPath(IList<Fix> path, string[] fixNames)
        {
            Assert.AreEqual(path.Count, fixNames.Length, "The path is shorter or longer than expected");
            for (int i = 0; i < path.Count; i++)
            {
                Assert.IsTrue(string.Equals(path[i].Name, fixNames[i], StringComparison.OrdinalIgnoreCase), $"Expected fix {fixNames[i]} but encoutered {path[i].Name}");
            }
        }
    }
}
