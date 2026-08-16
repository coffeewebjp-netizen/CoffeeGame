package jp.coffeetools.coffeegame.androidlib;

import android.app.Activity;
import android.content.Intent;
import android.os.Bundle;

public final class PickFolderActivity extends Activity {
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT_TREE);
        intent.addFlags(
            Intent.FLAG_GRANT_READ_URI_PERMISSION
                | Intent.FLAG_GRANT_WRITE_URI_PERMISSION
                | Intent.FLAG_GRANT_PERSISTABLE_URI_PERMISSION);
        startActivityForResult(intent, CloudFolder.REQUEST_CODE);
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode == CloudFolder.REQUEST_CODE && resultCode == RESULT_OK && data != null && data.getData() != null) {
            CloudFolder.persist(this, data.getData());
        }
        finish();
    }
}
