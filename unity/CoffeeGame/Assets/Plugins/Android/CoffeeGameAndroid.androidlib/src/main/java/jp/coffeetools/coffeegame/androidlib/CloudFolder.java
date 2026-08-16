package jp.coffeetools.coffeegame.androidlib;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.database.Cursor;
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
    public static final int REQUEST_CODE = 7101;

    private CloudFolder() {
    }

    public static String takeCoffeeGameLink(Activity activity) {
        if (activity == null) {
            return "";
        }

        Intent intent = activity.getIntent();
        if (intent == null || intent.getData() == null) {
            return "";
        }

        Uri data = intent.getData();
        if (data == null || !"coffeegame".equals(data.getScheme())) {
            return "";
        }

        String value = data.toString();
        Intent cleaned = new Intent(intent);
        cleaned.setData(null);
        activity.setIntent(cleaned);
        return value;
    }

    public static void pickFolder(Activity activity) {
        Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT_TREE);
        intent.addFlags(
            Intent.FLAG_GRANT_READ_URI_PERMISSION
                | Intent.FLAG_GRANT_WRITE_URI_PERMISSION
                | Intent.FLAG_GRANT_PERSISTABLE_URI_PERMISSION);
        activity.startActivityForResult(intent, REQUEST_CODE);
    }

    public static boolean hasFolder(Context context) {
        return getFolderUri(context) != null;
    }

    public static String getFolderLabel(Context context) {
        Uri uri = getFolderUri(context);
        return uri == null ? "" : uri.toString();
    }

    public static String writeTextResult(Context context, String name, String text) {
        Uri tree = getFolderUri(context);
        if (tree == null) {
            return "NO_FOLDER";
        }

        try {
            Uri document = DocumentsContract.buildDocumentUriUsingTree(
                tree,
                DocumentsContract.getTreeDocumentId(tree));
            Uri existing = findNamedFile(context, tree, DocumentsContract.getTreeDocumentId(tree), name, 3);
            Uri target = existing != null
                ? existing
                : DocumentsContract.createDocument(context.getContentResolver(), document, "application/json", name);
            if (target == null) {
                return "CREATE_FAILED";
            }

            OutputStream output = context.getContentResolver().openOutputStream(target, "rwt");
            if (output == null) {
                return "OPEN_FAILED";
            }
            try {
                output.write(text.getBytes(StandardCharsets.UTF_8));
            } finally {
                output.close();
            }
            return "OK";
        } catch (Exception exception) {
            return exception.getClass().getSimpleName() + ": " + exception.getMessage();
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
            Uri existing = findNamedFile(context, tree, DocumentsContract.getTreeDocumentId(tree), name, 3);
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

    public static void persist(Context context, Uri tree) {
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

    private static Uri findNamedFile(
        Context context,
        Uri tree,
        String parentDocumentId,
        String name,
        int remainingDepth) {
        Uri children = DocumentsContract.buildChildDocumentsUriUsingTree(tree, parentDocumentId);
        Cursor cursor = context.getContentResolver().query(
            children,
            new String[] {
                DocumentsContract.Document.COLUMN_DOCUMENT_ID,
                DocumentsContract.Document.COLUMN_DISPLAY_NAME,
                DocumentsContract.Document.COLUMN_MIME_TYPE
            },
            null,
            null,
            null);
        if (cursor == null) {
            return null;
        }

        try {
            java.util.ArrayList<String> directories = new java.util.ArrayList<>();
            while (cursor.moveToNext()) {
                String documentId = cursor.getString(0);
                String displayName = cursor.getString(1);
                String mime = cursor.getString(2);
                if (name.equals(displayName)
                    && !DocumentsContract.Document.MIME_TYPE_DIR.equals(mime)) {
                    return DocumentsContract.buildDocumentUriUsingTree(tree, documentId);
                }

                if (remainingDepth > 0 && DocumentsContract.Document.MIME_TYPE_DIR.equals(mime)) {
                    directories.add(documentId);
                }
            }

            for (String directoryId : directories) {
                Uri nested = findNamedFile(context, tree, directoryId, name, remainingDepth - 1);
                if (nested != null) {
                    return nested;
                }
            }
        } finally {
            cursor.close();
        }

        return null;
    }

}
