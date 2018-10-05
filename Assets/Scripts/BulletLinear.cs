using UnityEngine;


/// <summary>
/// 直進弾
/// </summary>
public sealed class BulletLinear : CachedBehaviour {
	#region DEFINE
	private static readonly Vector3 ROT_AXIS = Vector3.back;

	private const int GENERIC_PARAM_MAX = 8;    // 汎用パラメータ数
	private Vector3 DIRECT_FORWARD = Vector3.down;  // 弾前方

	public delegate void ExtendProc(float elapsedTime, BulletLinear bullet);
	#endregion


	#region MEMBER
	[System.NonSerialized]
	public Vector3 shotPoint = Vector3.zero;    // 発射地点
	[System.NonSerialized]
	public Vector3 direct = Vector3.down;   // ターゲット（進行方向）
	[System.NonSerialized]
	public float speed = 0f;    // 移動速度
	[System.NonSerialized]
	public Vector3 move = Vector3.zero; // 移動距離
	[System.NonSerialized]
	public int genericState = 0;    // 汎用stateレジスタ
	[System.NonSerialized]
	public int[] genericInt = new int[GENERIC_PARAM_MAX];   // 汎用intレジスタ
	[System.NonSerialized]
	public float[] genericFloat = new float[GENERIC_PARAM_MAX]; // 汎用floatレジスタ
	[System.NonSerialized]
	public Vector3 genericVector = Vector3.zero;    // 汎用ベクトル

	private SpriteRenderer spRenderer = null;   // 描画
	private float angle = 0f;                   // 回転角度
	private float omega = 0f;                   // 回転速度
	private int wakeUpNo = 0;                   // 起動No
	private float absOmega = 0;                 // 回転速度絶対値
	private Collision collision = null;         // 当たり判定

	private ExtendProc extendHandler = null;    // 拡張処理コールバック
	#endregion


	#region PROPERTY
	/// <summary> 描画レイヤー指定 </summary>
	public int sortingLayer { get { return this.spRenderer.sortingLayerID; } set { this.spRenderer.sortingLayerID = value; } }
	#endregion


	#region MAIN FUNCTION
	/// <summary>
	/// 派生クラスでの固有生成処理
	/// </summary>
	protected override void OnCreate() {
		this.spRenderer = this.GetComponent<SpriteRenderer>();
	}

	/// <summary>
	/// 派生クラスでの固有解放処理
	/// </summary>
	protected override void OnRelease() { }

	/// <summary>
	/// 派生クラスでの固有起動処理
	/// </summary>
	/// <param name="no">実行No.</param>
	protected override void OnAwake(int no) {
		this.wakeUpNo = no;
		this.genericState = 0;
	}

	/// <summary>
	/// 派生クラスでの固有終了処理
	/// </summary>
	protected override void OnSleep() {
		if (this.collision != null) {
			this.collision.Sleep();
			this.collision = null;
		}
		this.extendHandler = null;
	}

	/// <summary>
	/// 派生クラスでの固有更新処理(falseを返すと消滅)
	/// </summary>
	/// <param name="no">実行No.</param>
	/// <param name="elapsedTime">経過時間</param>
	protected override bool OnRun(int no, float elapsedTime) {
		// 描画順序
		this.spRenderer.sortingOrder = no;

		// 移動
		float speedTime = this.speed * elapsedTime;
		this.move.x += this.direct.x * speedTime;
		this.move.y += this.direct.y * speedTime;
		// 拡張処理
		if (this.extendHandler != null)
			this.extendHandler(elapsedTime, this);

		// 座標反映
		this.trans_.localPosition = this.move;
		// コリジョン
		// MEMO: 弾は当たり判定が途中で消されても稼動できるよう対応する
		if (this.collision != null) {
			this.collision.point = this.move;
			// Sprite座標からScreen座標に落とす
			this.collision.point.x += (float)Screen.width * 0.5f;
			this.collision.point.y += (float)Screen.height * 0.5f;
		}
		// 回転更新
		if (this.absOmega > DEFINE.FLOAT_MINIMUM) {
			this.angle += this.omega * elapsedTime;
			this.trans_.localRotation = Quaternion.AngleAxis(this.angle, ROT_AXIS);
		}

		bool ret = true;
		// 画面外判定…本来は画面サイズを固定した方がいい
		if (Mathf.Abs(this.move.x) > (Screen.width * 0.6f))
			ret = false;
		if (Mathf.Abs(this.move.y) > (Screen.height * 0.6f))
			ret = false;

		return ret;
	}
	#endregion


	#region PUBLIC FUNCTION
	/// <summary>
	/// 射撃
	/// </summary>
	/// <param name="sprite">スプライト</param>
	/// <param name="speed">速度（pix./sec.）</param>
	/// <param name="omega">回転速度（deg./sec.）</param>
	/// <param name="direct">進行方向</param>
	/// <param name="passedTime">発射時間から経ってしまった時間</param>
	public bool Shoot(Sprite sprite, float speed, float omega, ref Vector3 direct, float passedTime) {
		this.direct = direct;
		this.spRenderer.sprite = sprite;
		this.shotPoint = this.trans_.localPosition;

		this.speed = speed;
		this.omega = omega;
		this.absOmega = this.omega < 0f ? -this.omega : this.omega;

		// 向き
		this.angle = Vector3.Angle(DIRECT_FORWARD, this.direct);
		// MEMO: 回転軸が奥向きなので反時計回りが正方向
		if (this.direct.x > 0)
			this.angle = -this.angle;
		//MEMO: 回転弾はOnRun時に更新されるのでここで反映させない
		if (this.absOmega < DEFINE.FLOAT_MINIMUM)
			this.trans_.localRotation = Quaternion.AngleAxis(this.angle, ROT_AXIS);

		// 初期座標
		this.move = this.shotPoint;

		// コリジョン
		this.collision = GameManager.collision.PickOut(COL_CATEGORY.EN_BULLET, null);
		this.collision.SetCircle(15f);

		// MEMO: 通常可変フレームでこれをやると処理落ちした際にコリジョンが突き抜けるので衝突補正が必要になる
		// 今回は60FPSを下回った場合でもelapsedTimeが1/60秒を下回らないようにするので衝突補正はなくても許容範囲
		if (passedTime > DEFINE.FLOAT_MINIMUM) {
			this.OnRun(this.wakeUpNo, passedTime);
		} else {
			this.trans_.localPosition = this.move;
		}

		return true;
	}

	/// <summary>
	/// 拡張処理設定
	/// </summary>
	/// <param name="procedure">毎更新時に呼ばれる処理</param>
	public void ExtendCallback(ExtendProc procedure) {
		this.extendHandler = procedure;
	}

	/// <summary>
	/// コリジョン円形設定
	/// </summary>
	/// <param name="range">半径</param>
	public void CollisionCircle(float range) {
		this.collision.SetCircle(range);
	}

	/// <summary>
	/// コリジョン矩形設定
	/// </summary>
	/// <param name="width">幅</param>
	/// <param name="height">高さ</param>
	public void CollisionRect(float width, float height) {
		// 今回弾の回転軸をVector3.backにしているので左手座標系と回転角が逆になっている
		this.collision.SetRectangle(width, height, -this.angle);
	}
	#endregion
}
