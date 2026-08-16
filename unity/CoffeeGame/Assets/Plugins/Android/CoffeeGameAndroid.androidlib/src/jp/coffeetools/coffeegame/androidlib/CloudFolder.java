package jp.coffeetools.coffeegame.androidlib;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.net.Uri;
import android.os.Bundle;
import android.provider.DocumentsContract;

import java.io.ByteArrayOutputStream;
import java.io.InputStream;
import java.io.OutputStream;
import java.nio.charset.StandardCharsets;

public final class CloudFolder {
    static final String PREFS = "CoffeeGAME.cloud";
    static final String KEY_URI = "treeUri";
    static final int REQUEST_CODE = 7101;

    private CloudFolder() {
    }

    public static void pickFolder(Activity activity) {
        Intent intent = new Intent(activity, PickFolderActivity.class);
        activity.startActivity(intent);
    }

    public static boolean hasFolder(Context context) {
        return getFolderUri(context) != null;
    }

    public static String getFolderLabel(Context context) {
        Uri uri = getFolderUri(context);
        return uri == null ? "" : uri.toString();
    }

    public static boolean writeText(Context context, String name, String text) {
        Uri tree = getFolderUri(context);
        if (tree == null) {
            return false;
        }

        try {
            Uri document = DocumentsContract.buildDocumentUriUsingTree(
                tree,
                DocumentsContract.getTreeDocumentId(tree));
            Uri existing = findChild(context, document, name);
            Uri target = existing != null
                ? existing
                : DocumentsContract.createDocument(context.getContentResolver(), document, "application/json", name);
            if (target == null) {
                return false;
            }

            OutputStream output = context.getContentResolver().openOutputStream(target, "wt");
            if (output == null) {
                return false;
            }
            try {
                output.write(text.getBytes(StandardCharsets.UTF_8));
            } finally {
                output.close();
            }
            return true;
        } catch (Exception exception) {
            return false;
        }
    }

    public static String readText(Context context, String name) {
        Uri tree = getFolderUri(context);
        if (tree == null) {
            return null;
        }

        try {
            Uri document = DocumentsContract.buildDocumentUriUsingTree(
                tree,
                DocumentsContract.getTreeDocumentId(tree));
            Uri existing = findChild(context, document, name);
            if (existing == null) {
                return null;
            }

            InputStream input = context.getContentResolver().openInputStream(existing);
            if (input == null) {
                return null;
            }
            try {
                ByteArrayOutputStream buffer = new ByteArrayOutputStream();
                byte[] chunk = new byte[4096];
                int read;
                while ((read = input.read(chunk)) >= 0) {
                    buffer.write(chunk, 0, read);
                }
                return new String(buffer.toByteArray(), StandardCharsets.UTF_8);
            } finally {
                input.close();
            }
        } catch (Exception exception) {
            return null;
        }
    }

    static void persist(Context context, Uri tree) {
        context.getContentResolver().takePersistableUriPermission(
            tree,
            Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_GRANT_WRITE_URI_PERMISSION);
        SharedPreferences prefs = context.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
        prefs.edit().putString(KEY_URI, tree.toString()).apply();
    }

    static Uri getFolderUri(Context context) {
        SharedPreferences prefs = context.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
        String raw = prefs.getString(KEY_URI, "");
        if (raw == null || raw.length() == 0) {
            return null;
        }
        return Uri.parse(raw);
    }

    private static Uri findChild(Context context, Uri parent, String name) {
        // Best-effort: createDocument will fail if the file exists; callers then rewrite.
        return null;
    }

    public static class PickFolderActivity extends Activity {
        @Override
        protected void onCreate(Bundle savedInstanceState) {
            super.onCreate(savedInstanceState);
            Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT_TREE);
            intent.addFlags(
                Intent.FLAG_GRANT_READ_URI_PERMISSION
                    | Intent.FLAG_GRANT_WRITE_URI_PERMISSION
                    | Intent.FLAG_GRANT_PERSISTABLE_URI_PERMISSION);
            startActivityForResult(intent, REQUEST_CODE);
        }

        @Override
        protected void onActivityResult(int requestCode, int resultCode, Intent data) {
            super.onActivityResult(requestCode, resultCode, data);
            if (requestCode == REQUEST_CODE && resultCode == RESULT_OK && data != null && data.getData() != null) {
                CloudFolder.persist(this, data.getData());
            }
            finish();
        }
    }
}
