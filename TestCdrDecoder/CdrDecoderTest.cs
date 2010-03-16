using CDR.Decoder;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace TestCdrDecoder
{
    
    
    /// <summary>
    ///This is a test class for CdrDecoderTest and is intended
    ///to contain all CdrDecoderTest Unit Tests
    ///</summary>
    [TestClass()]
    public class CdrDecoderTest
    {


        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        #region Additional test attributes
        // 
        //You can use the following additional attributes as you write your tests:
        //
        //Use ClassInitialize to run code before running the first test in the class
        //[ClassInitialize()]
        //public static void MyClassInitialize(TestContext testContext)
        //{
        //}
        //
        //Use ClassCleanup to run code after all tests in a class have run
        //[ClassCleanup()]
        //public static void MyClassCleanup()
        //{
        //}
        //
        //Use TestInitialize to run code before running each test
        //[TestInitialize()]
        //public void MyTestInitialize()
        //{
        //}
        //
        //Use TestCleanup to run code after each test has run
        //[TestCleanup()]
        //public void MyTestCleanup()
        //{
        //}
        //
        #endregion

        /// <summary>
        ///A test for Dump
        ///</summary>
        public void DumpTest()
        {
            CdrDecoder target = new CdrDecoder();
            Stream asnStream = new FileStream(@"", FileMode.Open);
            int expected = 2; // BerDecoderResult.EOF
            int actual;
            TextWriter log = new StreamWriter(@"D:\Projects\Ericsson\log.txt");
            actual = target.Dump(asnStream, log, DumpType.ELEMENT_XML, byte.MaxValue);
            Assert.AreEqual(expected, actual);
        }

        /// <summary>
        ///A test for CheckElementsDefinition
        ///</summary>
        public void CheckElementsDefinitionTest()
        {
            CdrDecoder target = new CdrDecoder(); // TODO: Initialize to an appropriate value
            Stream asnStream = new FileStream(@"", FileMode.Open);
            TextWriter dumpWriter = new StreamWriter(@"log.txt");
            int expected = 2; // BerDecoderResult.EOF
            int actual;
            actual = target.CheckElementsDefinition(asnStream, dumpWriter);
            Assert.AreEqual(expected, actual);
        }
    }
}
