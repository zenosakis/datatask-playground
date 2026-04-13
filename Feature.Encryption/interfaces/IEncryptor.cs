namespace Feature.Encryption.interfaces
{
    /// <summary>
    /// 암호화 인터페이스. 인터페이스를 상속받은 클래스로 알고리즘 결정 (AES, RSA 등)
    /// </summary>
    public interface IEncryptor
    {

        private const string Prefix = "ENC(";
        private const string Suffix = ")";

        /// <summary>
        /// 단순 암호화
        /// </summary>
        /// <param name="plainText">암호화 할 평문</param>
        /// <returns></returns>
        string Encrypt(string plainText);

        /// <summary>
        /// 암호화 후 Prefix + 암호화값 + Suffix => ex) "hello" -> "ENC(Base64암호화값)"
        /// </summary>
        /// <param name="plainText">암호화 할 평문</param>
        /// <returns></returns>
        string Protect(string plainText)
        {
            return $"{Prefix}{Encrypt(plainText)}{Suffix}";
        }
        
        /// <summary>
        /// 단순 복호화 
        /// </summary>
        /// <param name="encryptionValue">복호화 할 암호화값</param>
        /// <returns></returns>
        string Decrypt(string encryptionValue);

        /// <summary>
        /// Prefix + 암호화값 + Suffix 를 복호화 => ex) "ENC(Base64암호화값)" -> "hello"
        /// </summary>
        /// <param name="encryptionValue"></param>
        /// <returns></returns>
        string Unprotect(string encryptionValue)
        {
            if (!encryptionValue.StartsWith(Prefix) || !encryptionValue.EndsWith(Suffix))
                return encryptionValue;
            return Decrypt(encryptionValue[Prefix.Length..^Suffix.Length]);
        }
    }
}
