set python_path=.\python
set python_bin=ipy.exe
set IRONPYTHONPATH=.\lib
set lib_path=lib

%python_path%\%python_bin%  -3 -X:Python30 -O -u  -X:ColorfulConsole generator.py %1 %2 %3 %4 %5 %6 %7 %8 %9
