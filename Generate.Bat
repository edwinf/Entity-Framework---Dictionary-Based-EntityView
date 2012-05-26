
set buildDir=..\..\..\..\Source\Domain\Model\bin\Debug\
"%windir%\Microsoft.NET\Framework\v4.0.30319\EdmGen.exe" /nologo /language:CSharp /mode:ViewGeneration "/inssdl:%buildDir%DomainModel.ssdl" "/incsdl:%buildDir%DomainModel.csdl" "/inmsl:%buildDir%DomainModel.msl" "/outviews:%buildDir%DomainModelEntityManager.Views.cs"

set outDir=..\..\..\..\Source\Domain\Model\EntityView\
..\..\Binaries\MakeRes.exe %buildDir%\DomainModelEntityManager.Views.cs %outDir% DomainModelEntityViews