# 🔐 AWS CREDENTIALS SETUP

## ⚠️ QUAN TRỌNG: KHÔNG BAO GIỜ COMMIT AWS CREDENTIALS VÀO GIT!

---

## 📋 CÁCH SETUP LOCAL

### **Bước 1: Copy appsettings template**

```bash
# File appsettings.Development.json và appsettings.json đã có placeholder
# KHÔNG sửa trực tiếp 2 file này!
```

---

### **Bước 2: Tạo file `appsettings.Local.json` (KHÔNG commit)**

Tạo file **`appsettings.Local.json`** trong thư mục `src/EVBSS.Api/`:

```json
{
  "AWS": {
    "Region": "ap-southeast-1",
    "AccessKey": "AKIAXXXXXXXXXXXXXXXX",
    "SecretKey": "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
  }
}
```

---

### **Bước 3: Thêm vào .gitignore**

File `.gitignore` cần có dòng này (đã có sẵn):

```
**/appsettings.Local.json
```

---

### **Bước 4: Update Program.cs (nếu chưa có)**

```csharp
var builder = WebApplication.CreateBuilder(args);

// Load appsettings.Local.json (optional, only in local dev)
builder.Configuration.AddJsonFile(
    "appsettings.Local.json", 
    optional: true, 
    reloadOnChange: true
);
```

---

## 🚀 PRODUCTION DEPLOYMENT

### **Option 1: Environment Variables (Recommended)**

Set environment variables trên server:

```bash
# Windows
setx AWS__REGION "ap-southeast-1"
setx AWS__ACCESSKEY "your-access-key"
setx AWS__SECRETKEY "your-secret-key"

# Linux
export AWS__REGION=ap-southeast-1
export AWS__ACCESSKEY=your-access-key
export AWS__SECRETKEY=your-secret-key
```

---

### **Option 2: Azure App Service Configuration**

1. Vào Azure Portal → App Service
2. **Configuration** → **Application settings**
3. Thêm:
   - `AWS:Region` = `ap-southeast-1`
   - `AWS:AccessKey` = `your-key`
   - `AWS:SecretKey` = `your-secret`

---

### **Option 3: AWS Secrets Manager (Best Practice)**

```csharp
// Install: dotnet add package AWS.Extensions.Configuration.SecretsManager

builder.Configuration.AddSecretsManager(
    region: Amazon.RegionEndpoint.APSoutheast1,
    secretName: "evbss/production/aws"
);
```

---

## ✅ VERIFICATION

### **Test AWS connection:**

```bash
dotnet run
# Check logs: Should NOT show "AccessKey: YOUR_AWS_ACCESS_KEY_HERE"
```

---

## 🔒 SECURITY CHECKLIST

- ✅ File `appsettings.Local.json` trong `.gitignore`
- ✅ File `appsettings.json` chỉ có placeholder
- ✅ KHÔNG commit AWS credentials vào Git
- ✅ Production dùng Environment Variables
- ✅ Rotate keys định kỳ (3-6 tháng)

---

## 🆘 NẾU ĐÃ COMMIT SECRETS

### **Bước 1: Remove secrets khỏi git history**

```bash
# Cách 1: Amend last commit (nếu chưa push)
git reset HEAD~1
# Sửa file, rồi commit lại

# Cách 2: BFG Repo-Cleaner (nếu đã push)
# Download: https://rtyley.github.io/bfg-repo-cleaner/
java -jar bfg.jar --replace-text passwords.txt
git reflog expire --expire=now --all
git gc --prune=now --aggressive
git push --force
```

---

### **Bước 2: Rotate AWS keys NGAY LẬP TỨC**

1. Vào AWS Console → IAM → Users
2. Delete old Access Key
3. Create new Access Key
4. Update `appsettings.Local.json`

---

## 📚 RELATED DOCS

- [AWS IAM Best Practices](https://docs.aws.amazon.com/IAM/latest/UserGuide/best-practices.html)
- [GitHub Secret Scanning](https://docs.github.com/code-security/secret-scanning)
- [.NET User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets)

---

## 🎯 TÓM TẮT

```
┌─────────────────────────────────────────────────┐
│  LOCAL DEV: appsettings.Local.json (gitignore)  │
│  PRODUCTION: Environment Variables / Azure Config│
│  ⚠️ NEVER: Commit secrets to Git                 │
└─────────────────────────────────────────────────┘
```

