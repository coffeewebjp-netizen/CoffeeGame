package jp.coffeetools.coffeegame.androidlib;

import android.security.keystore.KeyGenParameterSpec;
import android.security.keystore.KeyProperties;
import android.util.Base64;

import java.nio.charset.StandardCharsets;
import java.security.KeyStore;

import javax.crypto.Cipher;
import javax.crypto.KeyGenerator;
import javax.crypto.SecretKey;
import javax.crypto.spec.GCMParameterSpec;

public final class TokenVault {
    private static final String ANDROID_KEY_STORE = "AndroidKeyStore";
    private static final String KEY_ALIAS = "CoffeeGAME.CoffeeLearning.cgt.v1";
    private static final String TRANSFORMATION = "AES/GCM/NoPadding";
    private static final int GCM_TAG_LENGTH = 128;

    private TokenVault() {
    }

    public static String protectText(String plaintext) throws Exception {
        byte[] packed = protect(plaintext.getBytes(StandardCharsets.UTF_8));
        return Base64.encodeToString(packed, Base64.NO_WRAP);
    }

    public static String unprotectText(String packed) throws Exception {
        byte[] plain = unprotect(Base64.decode(packed, Base64.NO_WRAP));
        return new String(plain, StandardCharsets.UTF_8);
    }

    public static byte[] protect(byte[] plaintext) throws Exception {
        SecretKey key = getOrCreateKey();
        Cipher cipher = Cipher.getInstance(TRANSFORMATION);
        cipher.init(Cipher.ENCRYPT_MODE, key);
        byte[] iv = cipher.getIV();
        byte[] cipherText = cipher.doFinal(plaintext);
        byte[] packed = new byte[1 + iv.length + cipherText.length];
        packed[0] = (byte) iv.length;
        System.arraycopy(iv, 0, packed, 1, iv.length);
        System.arraycopy(cipherText, 0, packed, 1 + iv.length, cipherText.length);
        return packed;
    }

    public static byte[] unprotect(byte[] packed) throws Exception {
        if (packed == null || packed.length < 13) {
            throw new IllegalArgumentException("Packed credential is too short.");
        }

        int ivLength = packed[0] & 0xff;
        if (ivLength < 12 || packed.length < 1 + ivLength + 16) {
            throw new IllegalArgumentException("Packed credential IV is invalid.");
        }

        byte[] iv = new byte[ivLength];
        System.arraycopy(packed, 1, iv, 0, ivLength);
        byte[] cipherText = new byte[packed.length - 1 - ivLength];
        System.arraycopy(packed, 1 + ivLength, cipherText, 0, cipherText.length);

        SecretKey key = getOrCreateKey();
        Cipher cipher = Cipher.getInstance(TRANSFORMATION);
        cipher.init(Cipher.DECRYPT_MODE, key, new GCMParameterSpec(GCM_TAG_LENGTH, iv));
        return cipher.doFinal(cipherText);
    }

    private static SecretKey getOrCreateKey() throws Exception {
        KeyStore keyStore = KeyStore.getInstance(ANDROID_KEY_STORE);
        keyStore.load(null);
        if (keyStore.containsAlias(KEY_ALIAS)) {
            return ((KeyStore.SecretKeyEntry) keyStore.getEntry(KEY_ALIAS, null)).getSecretKey();
        }

        KeyGenerator generator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, ANDROID_KEY_STORE);
        generator.init(
            new KeyGenParameterSpec.Builder(
                KEY_ALIAS,
                KeyProperties.PURPOSE_ENCRYPT | KeyProperties.PURPOSE_DECRYPT)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                .setRandomizedEncryptionRequired(true)
                .build());
        return generator.generateKey();
    }
}
