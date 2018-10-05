using UnityEngine;


[DefaultExecutionOrder(-1)]
public class GameManager : MonoBehaviour {
    public static CollisionPool collision;
	public static ObjectPool<BulletLinear> bulletManager;

	[SerializeField]
	private GameObject bulletPrefab = null;
	[SerializeField]
	private Camera spriteCamera = null;

#if DEBUG
	private Camera debugCamera = null;
	public bool displayCollision = false;
#endif

	void Awake() {
        GameManager.collision = new CollisionPool();
        GameManager.collision.Initialize();

		GameManager.bulletManager = new ObjectPool<BulletLinear>();
		bulletManager.Initialize(2, this.bulletPrefab, 2000);
		bulletManager.Generate();

		this.spriteCamera.orthographicSize = Screen.height / 2;
		Application.targetFrameRate = 60;
	}

	void OnDestroy() {
        GameManager.collision.Final();
	}

	void Start() {
        // 1m = 1pixの位置にカメラを移動させる
        Camera cam = Camera.main;
        float dist = ((float)Screen.height * 0.5f) / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        cam.transform.localPosition = new Vector3(0f, 0f, -dist);

#if DEBUG
		GameObject deb = new GameObject("DebugCamera");
		this.debugCamera = deb.AddComponent<Camera>();
		this.debugCamera.CopyFrom(cam);
		this.debugCamera.clearFlags = CameraClearFlags.Depth;
		this.debugCamera.transform.SetParent(cam.transform, false);
		this.debugCamera.cullingMask = LayerMask.GetMask("Debug");
		this.debugCamera.depth = 9999;
#endif
    }

	void LateUpdate() {
		// MEMO: 固定時間フレーム
        float elapsedTime = DEFINE.FRAME_TIME_60;

		// 弾丸の更新
		GameManager.bulletManager.Proc(elapsedTime);

		// コリジョンの更新（判定）
#if DEBUG
		GameManager.collision.debugCamera = (this.displayCollision ? this.debugCamera : null);
		this.debugCamera.enabled = this.displayCollision;
#endif
		GameManager.collision.Proc(elapsedTime);
    }
}
