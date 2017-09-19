//------------------------------------------------------------------------------
//
// Copyright (c) Microsoft Corporation.
// All rights reserved.
//
// This code is licensed under the MIT License.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files(the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and / or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions :
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using ApiCheck;
using ApiCheck.Configuration;
using ApiCheck.Loader;
using ApiCheck.Result.Difference;
using Xunit;

namespace ApiChangeTest
{
    public class ApiChangeTest
    {        
        void RunApiCheck(string testId, string refAssemblyPath, string devAssemblyPath)
        {
            Console.WriteLine("====================================");
            Console.WriteLine(">>>> " + testId + Environment.NewLine);

            var sb = new StringBuilder();
            byte[] resultXml = null;
            var succeed = true;

            try
            {
                using (AssemblyLoader assemblyLoader = new AssemblyLoader())
                {
                    // load assemblies
                    Assembly refAssembly = assemblyLoader.ReflectionOnlyLoad(refAssemblyPath);
                    Assembly devAssembly = assemblyLoader.ReflectionOnlyLoad(devAssemblyPath);

                    // configuration
                    ComparerConfiguration configuration = new ComparerConfiguration();
                    configuration.Severities.ParameterNameChanged = Severity.Warning;
                    configuration.Severities.AssemblyNameChanged = Severity.Hint;
                    
                    // compare assemblies and write xml report
                    using (var stream = new MemoryStream())
                    {
                        ApiComparer.CreateInstance(refAssembly, devAssembly)
                          .WithComparerConfiguration(configuration)
                          .WithDetailLogging(s => Console.WriteLine(s))
                          .WithInfoLogging(s => Console.WriteLine(s))
                          .WithXmlReport(stream)
                          .Build()
                          .CheckApi();

                        resultXml = stream.ToArray();
                    }
                }

                var scenarioList = new List<string>() { "ChangedAttribute", "ChangedElement", "RemovedElement" };

                // check the scenarios that we might have a breaking change
                foreach (var scenario in scenarioList)
                {
                    using (var stream = new MemoryStream(resultXml))
                    {
                        XElement doc = XElement.Load(stream);

                        foreach (XElement change in doc.Descendants(scenario))
                        {
                            if (change.Attribute("Severity") != null && "Error".Equals(change.Attribute("Severity").Value))
                            {
                                succeed = false;

                                // append the parent, for instance, 
                                if (change.Parent != null)
                                    sb.AppendLine($"In {change.Parent.Attribute("Context").Value} : {change.Parent.Attribute("Name").Value}");

                                sb.AppendLine(change.ToString());
                            }
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                throw new ApiChangeException("Assembly comparison failed.", ex);
            }

            if (!succeed)
                throw new ApiChangeException($"The following breaking changes are found: {Environment.NewLine} {sb.ToString()}");
        }

        [Fact]
        public void MicrosoftIdentityModelTokensApiTest()
        {
            var refAssemblyPath = Path.Combine(Directory.GetCurrentDirectory(), @"Resource\Microsoft.IdentityModel.Tokens.512.dll");
            var devAssemblyPath = Path.Combine(Directory.GetCurrentDirectory(), @"..\..\..\..\..\src\Microsoft.IdentityModel.Tokens\bin\Debug\net451\Microsoft.IdentityModel.Tokens.dll");
            RunApiCheck($"{this}.MicrosoftIdentityModelTokensApiTest", refAssemblyPath, devAssemblyPath);
        }
    }
}
