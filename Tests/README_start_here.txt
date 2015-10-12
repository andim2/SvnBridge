This is a short tutorial of the unit tests
contained in the SvnBridge project.

Unit tests xUnit http://xunit.codeplex.com/wikipage?title=HowToUse

In a cmd / Visual Studio command prompt, you may run
    \path\to\xunit.console.exe Tests.dll
to execute all tests in Tests.dll
(and be sure to sometimes execute the other test .dll:s, too!!)
, and optionally with /trait "TestName=HRCR"
to achieve single-test execution,
after having decorated e.g. the Handle_ReturnsCorrectResponse() test
with a leading
  [Trait("TestName", "HRCR")]
line.

The usual location of xunit.console.exe will be
the project-included binaries within our References folder,
however one may also register an external standard xUnit installation
in the GAC,
which then is to be used for test execution, too.

Rather than using xunit.console.exe,
one may also use xunit.gui.exe instead.


!!! You should try hard to make sure to run all unit tests
!!! whenever having changed something.


Debugging of unit test code may e.g. be done in a Q&D manner
by attaching on xunit.gui.exe,
setting a couple breakpoints in SvnBridge code,
then executing various tests within xunit.gui.exe.


Also, it seems that some of these tests are indeed meant to be invoked (read: verified)
against an actual original SVN instance as well (TODO: how?),
as indicated by some commit logs and changes of Subversion version IDs in the tests.

HTH,

Andreas Mohr
