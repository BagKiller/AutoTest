@echo off
echo === Building VegaAgent ===
pyinstaller --onefile --noconsole ^
  --add-data "templates;templates" ^
  --add-data "static;static" ^
  --add-data "models.json;." ^
  --name VegaAgent ^
  app.py
echo === Done ===
pause
