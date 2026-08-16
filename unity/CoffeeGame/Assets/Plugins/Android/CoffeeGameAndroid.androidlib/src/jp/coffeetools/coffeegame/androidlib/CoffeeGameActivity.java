package jp.coffeetools.coffeegame.androidlib;

import android.content.Intent;

import com.unity3d.player.UnityPlayerGameActivity;

public class CoffeeGameActivity extends UnityPlayerGameActivity {
    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode == CloudFolder.REQUEST_CODE
            && resultCode == RESULT_OK
            && data != null
            && data.getData() != null) {
            CloudFolder.persist(this, data.getData());
        }
    }
}
