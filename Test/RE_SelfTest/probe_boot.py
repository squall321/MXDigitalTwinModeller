# encoding: utf-8
from System.IO import File
from System.Text import UTF8Encoding
File.WriteAllText(r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\boot_mark.txt", "alive\n", UTF8Encoding(False))
import clr
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
File.AppendAllText(r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\boot_mark.txt", "dll-loaded\n", UTF8Encoding(False))
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import LlmToolDispatcher
File.AppendAllText(r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\boot_mark.txt", "dispatcher-imported\n", UTF8Encoding(False))
