using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using atc.utilities;

namespace AirTrafficControl.Tests
{
    [TestClass]
    public class ErrorPolicyTests
    {
        [TestMethod]
        public async Task RequestErrorPolicySuppressesCancellationExceptions()
        {
            // These two should not throw and the test should pass
            await ErrorHandlingPolicy.ExecuteRequestAsync(() => { throw new OperationCanceledException(); });
            await ErrorHandlingPolicy.ExecuteRequestAsync(() => { throw new TaskCanceledException(); });
        }

        [TestMethod]
        public async Task RequestErrorPolicyIgnoresNonCancellationExceptions()
        {
            await Assert.ThrowsExceptionAsync<Exception>(() => ErrorHandlingPolicy.ExecuteRequestAsync(() => { throw new Exception(); }));
        }
    }
}
