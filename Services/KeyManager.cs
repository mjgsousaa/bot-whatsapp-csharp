using System;
using System.Security.Cryptography;
using System.IO;
using System.Text;

namespace BotWhatsappCSharp.Services
{
    public class KeyManager
    {
        // Mesma chave do KeyGen (CORRIGIDA)
        private const string SecretKey = "TxOpq7sL8wZ9xK2mN4pQ7vR3zC6dL9xJ2kM5nB8vA1E=";

        // URL e apikey da SUA Evolution API (Embutidas/Ocultas)
        internal static readonly string EvoBaseUrl = "https://evolutionapi.vps7841.panel.icontainer.cloud";
        internal static readonly string EvoApiKey  = "wFRfS5EhXJkbC6FKJi7fDYDsFwsbWkYD";

        // Gera nome de instância único baseado na chave
        public static string GerarNomeInstancia(string licenseKey)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            byte[] hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(licenseKey));
            // Pega 8 caracteres hex do hash — único por chave
            string hex = BitConverter.ToString(hash).Replace("-", "").ToLower().Substring(0, 8);
            return $"botzap-{hex}";
        }

        public KeyResult ValidarChave(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return new KeyResult(false, "Por favor, insira uma chave.", "", "", "");

            // Backdoor Admin (Opcional, pode remover se quiser)
            if (token.Equals("BOTZAP-ADMIN-MASTER", StringComparison.OrdinalIgnoreCase))
                return new KeyResult(true, "Acesso Mestre", "ADMIN", "Vitalício");

            try
            {
                string decrypted = Decrypt(token);
                // Payload esperado: "CLIENTE|DATA" (Ex: "JoaoLoja|2025-12-31")
                var parts = decrypted.Split('|');
                
                if (parts.Length < 2) return new KeyResult(false, "Formato de chave inválido.", "", "", "");

                string cliente = parts[0];
                string plano = parts.Length >= 3 ? parts[2].ToLower() : "basico";

                if (DateTime.TryParse(parts[1], out DateTime validade))
                {
                    if (validade >= DateTime.Now.Date)
                    {
                        return new KeyResult(true, $"Licenciado para: {cliente}", cliente, validade.ToShortDateString(), plano);
                    }
                    else
                    {
                        return new KeyResult(false, $"Licença expirada em {validade:dd/MM/yyyy}", cliente, null);
                    }
                }
                return new KeyResult(false, "Data de validade inválida na chave.", "", "", "");
            }
            catch
            {
                return new KeyResult(false, "Chave inválida ou corrompida.", null, null);
            }
        }

        private string Decrypt(string cipherText)
        {
            byte[] fullCipher = Convert.FromBase64String(cipherText);
            byte[] key = Convert.FromBase64String(SecretKey);
            
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                
                // Extrair IV (primeiros 16 bytes)
                byte[] iv = new byte[16];
                Array.Copy(fullCipher, 0, iv, 0, iv.Length);
                aes.IV = iv;

                // O resto é o texto cifrado
                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                using (MemoryStream msDecrypt = new MemoryStream(fullCipher, 16, fullCipher.Length - 16))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                        {
                            return srDecrypt.ReadToEnd();
                        }
                    }
                }
            }
        }

        public class KeyResult
        {
            public bool Valido { get; }
            public string Mensagem { get; }
            public string Tipo { get; }
            public string Exp { get; }
            public string Plano { get; }

            public KeyResult(bool valido, string mensagem, string tipo, string exp, string plano = "")
            {
                Valido = valido;
                Mensagem = mensagem;
                Tipo = tipo;
                Exp = exp;
                Plano = plano;
            }
        }
    }
}
