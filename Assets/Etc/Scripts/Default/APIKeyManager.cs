using System;
using System.IO;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class APIKeyManager : MonoBehaviour
{
    public static APIKeyManager Instance { get; private set; }

    [Header("첫 실행용 원본 auth.json")]
    [SerializeField] private TextAsset bootstrapAuthJson;

    private string authFilePath;
    private AuthData cachedAuthData;

    public bool IsInitialized { get; private set; }
    public bool HasValidAuth => cachedAuthData != null && !string.IsNullOrWhiteSpace(cachedAuthData.ApiKey);
    public string LastError { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Initialize();
    }

    public bool Initialize()
    {
        if (IsInitialized)
            return HasValidAuth;

        LastError = null;

        try
        {
            string targetFolder = GetOpenAIFolderPath();
            authFilePath = Path.Combine(targetFolder, "auth.json");

            if (TryLoadEncryptedAuthFile(authFilePath, out cachedAuthData))
            {
                IsInitialized = true;
                Debug.Log($"Encrypted auth file loaded successfully: {authFilePath}");
                return true;
            }

            Debug.Log("Encrypted auth file was not found or invalid. Trying to create a new one.");

            if (TryCreateEncryptedAuthFile())
            {
                if (TryLoadEncryptedAuthFile(authFilePath, out cachedAuthData))
                {
                    IsInitialized = true;
                    Debug.Log($"Encrypted auth file created and verified successfully: {authFilePath}");
                    return true;
                }

                LastError = "auth.json was created, but verification failed.";
                Debug.LogError(LastError);
                IsInitialized = true;
                return false;
            }

            IsInitialized = true;
            return false;
        }
        catch (Exception ex)
        {
            LastError = $"Initialize failed: {ex.Message}";
            Debug.LogError(LastError);
            IsInitialized = true;
            return false;
        }
    }

    public bool TryGetAuthData(out AuthData authData)
    {
        if (!IsInitialized)
            Initialize();

        authData = cachedAuthData;
        return authData != null && !string.IsNullOrWhiteSpace(authData.ApiKey);
    }

    public string GetAuthFilePath()
    {
        if (string.IsNullOrWhiteSpace(authFilePath))
        {
            string targetFolder = GetOpenAIFolderPath();
            authFilePath = Path.Combine(targetFolder, "auth.json");
        }

        return authFilePath;
    }

    private string GetOpenAIFolderPath()
    {
        string userFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string targetFolder = Path.Combine(userFolder, ".openai");

        Directory.CreateDirectory(targetFolder);
        Debug.Log($"Folder verification and creation complete: {targetFolder}");

        return targetFolder;
    }

    private bool TryCreateEncryptedAuthFile()
    {
        AuthData sourceAuthData = null;

        if (TryLoadBootstrapAuthFromTextAsset(out sourceAuthData))
        {
            return SaveEncryptedAuthFile(sourceAuthData);
        }

        if (TryLoadBootstrapAuthFromEnvironment(out sourceAuthData))
        {
            return SaveEncryptedAuthFile(sourceAuthData);
        }

        LastError =
            "No bootstrap auth source found. " +
            "Assign bootstrapAuthJson in the inspector or provide OPENAI_API_KEY environment variable.";

        Debug.LogError(LastError);
        return false;
    }

    private bool TryLoadBootstrapAuthFromTextAsset(out AuthData authData)
    {
        authData = null;

        if (bootstrapAuthJson == null)
        {
            Debug.LogWarning("bootstrapAuthJson is not assigned.");
            return false;
        }

        string json = bootstrapAuthJson.text;
        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogError("bootstrapAuthJson is empty.");
            return false;
        }

        try
        {
            authData = JsonUtility.FromJson<AuthData>(json);
            if (!IsValidAuthData(authData))
            {
                Debug.LogError("bootstrapAuthJson format is invalid or API key is empty.");
                authData = null;
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to parse bootstrapAuthJson: {ex.Message}");
            authData = null;
            return false;
        }
    }

    private bool TryLoadBootstrapAuthFromEnvironment(out AuthData authData)
    {
        authData = null;

        string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        string organization = Environment.GetEnvironmentVariable("OPENAI_ORGANIZATION");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return false;
        }

        authData = new AuthData
        {
            ApiKey = apiKey.Trim(),
            Organization = string.IsNullOrWhiteSpace(organization) ? "" : organization.Trim()
        };

        return true;
    }

    private bool SaveEncryptedAuthFile(AuthData authData)
    {
        if (!IsValidAuthData(authData))
        {
            Debug.LogError("Cannot save auth file because auth data is invalid.");
            return false;
        }

        try
        {
            string encryptedJsonContent = EncryptionHelper.Encrypt(authData.GetEncryptedJson());
            File.WriteAllText(GetAuthFilePath(), encryptedJsonContent);

            bool existsAfterWrite = File.Exists(GetAuthFilePath());
            if (!existsAfterWrite)
            {
                Debug.LogError("auth.json write completed, but file does not exist afterwards.");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to save encrypted auth file: {ex.Message}");
            return false;
        }
    }

    private bool TryLoadEncryptedAuthFile(string filePath, out AuthData authData)
    {
        authData = null;

        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        if (!File.Exists(filePath))
            return false;

        try
        {
            string encryptedContent = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(encryptedContent))
            {
                Debug.LogWarning("Encrypted auth file exists but is empty.");
                return false;
            }

            string decryptedJson = EncryptionHelper.Decrypt(encryptedContent);
            if (string.IsNullOrWhiteSpace(decryptedJson))
            {
                Debug.LogWarning("Decrypted auth content is empty.");
                return false;
            }

            authData = AuthData.FromDecryptedJson(decryptedJson);
            if (!IsValidAuthData(authData))
            {
                Debug.LogWarning("Decrypted auth data is invalid.");
                authData = null;
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to read encrypted auth file: {ex.Message}");
            return false;
        }
    }

    private bool IsValidAuthData(AuthData authData)
    {
        return authData != null && !string.IsNullOrWhiteSpace(authData.ApiKey);
    }
}

[Serializable]
public class AuthData
{
    [SerializeField] private string api_key;
    [SerializeField] private string organization;

    public string ApiKey
    {
        get => api_key;
        set => api_key = value;
    }

    public string Organization
    {
        get => organization;
        set => organization = value;
    }

    public string GetEncryptedJson()
    {
        return JsonUtility.ToJson(this);
    }

    public static AuthData FromDecryptedJson(string json)
    {
        return JsonUtility.FromJson<AuthData>(json);
    }
}