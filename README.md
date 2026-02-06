To run this code locally in your Visual Studio IDE do the following:
  1. copy the AICodePresentationPublic.cs file into a test project in a solution
  2. install NuGet packages as required and prompted (NUnit, Playwright)
  3. replace the api endpoint and subscription key with your own associated with the Computer Vision Resource you have in Azure Portal. This will have to be created, if it does not exist (refer to Azure Portal documentation)
  4. copy the baselineImage.png file to the local baseline file location defined in the [Test]
     BaselineDir = C:\QAAutomation\TestInput\BaselineImages\ImgCompareMethodTests
  5.   Build and Run/Debug the test in Test Explorer
The cosine similarity value and tolerance will be output to console along with pass/fail
Ouput, including the snapshots taken in the initial implementation, will be written to the archive directory defined in the [Test]
  ArchiveDir = C:\QAAutomation\TestOutput\Snapshots\ImgCompareMethodTests\VisionEvalTestSnapshots

This code is core implementation code, requiring application extensions, such as security, error handling, logging, reporting, data stores, analytics to be useful in scalable, enterprise applications.
This code has been provided by Engineering at Blue Ridge Gateways Automation AI as open source courtesy, while it implements it into its Enterprise Ready Validation and Automation Solutions.
Copyright 2026 Blue Ridge Gateways, LLC
https://www.BlueRidgeGateways.com    
https://www.linkedin.com/company/blueridgegateways    
CTO https://www.linkedin.com/in/andreaaltenkirch/
