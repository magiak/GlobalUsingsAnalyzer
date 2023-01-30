using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using VerifyCS = GlobalUsingsAnalyzer.Test.CSharpCodeFixVerifier<
    GlobalUsingsAnalyzer.GlobalUsingsAnalyzerAnalyzer,
    GlobalUsingsAnalyzer.CSharpAddGlobalImportCodeFixProvider>;

namespace GlobalUsingsAnalyzer.Test
{
    [TestClass]
    public class CSharpAddGlobalImportCodeFixProviderUnitTests
    {
        [TestMethod]
        public async Task TestMethod3()
        {
            var test = @"
namespace GlobalUsingsAnalyzer.Test.Examples
{
    public class MyService
    {
    }
}

namespace GlobalUsingsAnalyzer.Test.ExamplesTwo
{
    public class Test
    {
        public void Method()
        {
            var myService = new {|#0:TypeName|}(); // missing using
        }
    }
}";

            var fixtest = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class TYPENAME
        {
        }
    }";

            var expected = VerifyCS.Diagnostic("CS0246").WithLocation(0).WithArguments("TypeName");
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
        }
    }
}