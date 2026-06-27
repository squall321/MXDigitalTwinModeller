$args = @('-NoProfile','-ExecutionPolicy','Bypass','-File',
          'D:\MXDigitalTwinModeller\Test\RE_SelfTest\run_mod_matrix.ps1',
          '-PerCellTimeoutSec','300')
& powershell.exe @args
