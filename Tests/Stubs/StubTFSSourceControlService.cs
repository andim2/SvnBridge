using System;
using System.Collections.Generic;
using System.Text;
using SvnBridge.SourceControl;

namespace Tests
{
    public class StubTFSSourceControlService : TFSSourceControlService
    {
        public StubTFSSourceControlService() : base(null, null, null, null, null) { }
    }
}
