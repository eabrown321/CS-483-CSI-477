# CS-483-CSI-477
Senior Software Development Projects - Ai Academic Advisor

# 1 ensure you are in the correct address 

# 2 Create "new" appsettings.json

# **IMPORTANT** This file is NOT in Git for security reasons. You must create it manually.
# NEVER commit appsettings.json to Git
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "YOUR_DB_CONNECTION_STRING_HERE;"
  },
  "AzureBlobStorage": {
    "ConnectionString": "YOUR_AZURE_KEY_HERE",
    "BulletinContainer": "bulletins",
    "MinorsContainer": "minors",
    "SupportingDocsContainer": "supporting-docs"
  },
  "FileUpload": {
    "MaxFileSizeMB": 50,
    "AllowedExtensions": [ ".pdf", ".docx", ".doc", ".txt" ]
  },
  "Gemini": {
    "Model": "gemini-2.5-flash",
    "FallbackModel": "gemini-2.5-flash-lite",
    "ApiKey": "YOUR_API_KEY_HERE",
    "ApiBaseUrl": "https://generativelanguage.googleapis.com/v1beta"
  },
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": "587",
    "SmtpUser": "",
    "SmtpPassword": "",
    "FromAddress": "noreply@acadadvising.com",
    "FromName": "AI Academic Advisor"
  },
  "AppUrl": "https://localhost:7192",
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}

```

# 3 restore nuGet packages and build project



## Passwords - (dummy for now)
Student
User: 1
Pass: password123

Admin
User: admin
Pass: admin123

# Common errors encountered

## "Database connection failed"
- Make sure you have the correct MySQL connection string

## "Gemini API error: 403 Forbidden"
- API Key was leaked, needs to be rotated and replaced

## "Cannot find appsettings.json" OR "ArgumentNullException (parameter[connectionstring])"
- You need to create the appsettings file manually
- YOu need to insert the correct credentials into the appsettings file
- You need to enter the Azure credentials into the AdminDashboard.cshtml.cs file
