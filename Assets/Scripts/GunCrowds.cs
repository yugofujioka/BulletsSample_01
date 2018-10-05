using UnityEngine;


/// <summary>
/// 殺到撃ち
/// </summary>
public sealed class GunCrowds : MonoBehaviour {
	#region DEFINE
	private const float SHOT_SPEED = 200f;				// 発射速度（pix./sec.）
	private const float	SHOT_SPAN = DEFINE.FRAME_TIME_60 * 22;	// 発射間隔（sec.）
	private const int	WAY_COUNT = 16;					// WAY数
	private const float	WAY_ANGLE = 360f / WAY_COUNT;	// WAY間隔（deg.）
	private const int	RAPID_COUNT = 4;				// 連射数
	private static readonly Quaternion WAY_START_ROT = Quaternion.AngleAxis(WAY_ANGLE * 0.5f, Vector3.back);
	private static readonly Quaternion WAY_ROT = Quaternion.AngleAxis(WAY_ANGLE, Vector3.back);

	private const float BRANCH_SPEED = 150f;						// 枝弾速度（pix./sec.）
	private const float BRANCH_DELAY = DEFINE.FRAME_TIME_60 * 35;	// 発射遅延時間（sec.）
	private const float	BRANCH_SPAN = DEFINE.FRAME_TIME_60 * 5;		// 連射間隔（sec.）
	private static readonly Quaternion BRANCH_ROT = Quaternion.AngleAxis(-2f, Vector3.back);	// 連射毎の旋回角度（deg.）
	private static readonly Quaternion BRANCH_ROT_INV = Quaternion.AngleAxis(2f, Vector3.back);	// 連射毎の旋回角度（deg.）
	#endregion


	#region MEMBER
	[SerializeField, Tooltip("主弾")]
	private Sprite mainBullet = null;
	[SerializeField, Tooltip("枝弾")]
	private Sprite branchBullet = null;

	private bool fire = false;		// 射撃フラグ
	private float shotWait = 0f;	// 発射待ち時間（sec.）
	private int rapidCount = 0;		// 連射数
		
	private BulletLinear.ExtendProc extendHandler = null;	// 弾の拡張処理
	#endregion


	#region MAIN FUNCTION
	/// <summary>
	/// 初期化
	/// </summary>
	public void Initialize() {
		this.extendHandler = new BulletLinear.ExtendProc(this.ExtendShot);
	}

	/// <summary>
	/// 稼動
	/// </summary>
	public void Run(float elapsedTime) {
		if (!this.fire)
			return;
			
		this.ProcMain(elapsedTime);
	}
	#endregion


	#region PUBLIC FUNCTION
	/// <summary>
	/// 射撃開始
	/// </summary>
	public void PullTrigger() {
		this.fire = true;
		this.shotWait = DEFINE.FRAME_TIME_60;
		this.rapidCount = 0;
	}

	/// <summary>
	/// 射撃停止
	/// </summary>
	public void ReleaseTrigger() {
		this.fire = false;
	}
	#endregion


	#region PRIVATE FUNCTION
	/// <summary>
	/// メイン射撃処理
	/// </summary>
	/// <param name="elapsedTime">経過時間</param>
	private void ProcMain(float elapsedTime) {
		this.shotWait -= elapsedTime;
		if (this.shotWait > DEFINE.FLOAT_MINIMUM)
			return;
			
		float passedTime = -this.shotWait;
		this.shotWait = SHOT_SPAN;
		this.ShotMain(passedTime);

		// 超過時間分を再帰計算
		if (passedTime > DEFINE.FLOAT_MINIMUM)
			this.ProcMain(passedTime);
	}

	/// <summary>
	/// メイン射撃
	/// </summary>
	/// <param name="passedTime">超過時間</param>
	private void ShotMain(float passedTime) {
		BulletLinear bullet = null;
		// 射線計算
		Vector3 dir = Vector3.down;
		Vector3 point = Camera.main.WorldToScreenPoint(this.transform.localPosition);
		point.x -= Screen.width * 0.5f;
		point.y -= Screen.height * 0.5f;
		point.z = 0f;
		// 3射目以降は半角ずらす
		if (this.rapidCount < 2)
			dir = WAY_START_ROT * dir;
		for (int i = 0; i < WAY_COUNT; ++i) {
			if (GameManager.bulletManager.AwakeObject(0, point, out bullet)) {
				BulletLinear bl = bullet as BulletLinear;
				bl.ExtendCallback(this.extendHandler);
				bl.genericFloat[0] = DEFINE.FRAME_TIME_60 + BRANCH_DELAY;
				bl.genericVector = -dir;
				bl.genericState = (this.rapidCount % 2) == 0 ? 0 : 1;	// 奇数発と偶数発を保持
				bl.Shoot(this.mainBullet, SHOT_SPEED, 1080f, ref dir, passedTime);
				// 本来はIDをキャッシュなり静的に定義するなりする
				bl.sortingLayer = SortingLayer.NameToID("BulletMain");
			} else {
				Debug.LogError("弾切れ : LINEAR");
				break;
			}
			dir = WAY_ROT * dir;
		}
		if (++this.rapidCount == RAPID_COUNT)
			this.ReleaseTrigger();
	}

	/// <summary>
	/// 弾から弾
	/// </summary>
	/// <param name="elapsedTime">経過時間</param>
	/// <param name="bullet">親弾</param>
	private void ExtendShot(float elapsedTime, BulletLinear bullet) {
		bullet.genericFloat[0] -= elapsedTime;
		if (bullet.genericFloat[0] > DEFINE.FLOAT_MINIMUM)
			return;

		Vector3 dir = bullet.genericVector;
		Vector3 point = bullet.move + dir * 12f; // 細長いので発生位置をズラす
		BulletLinear bl;
		if (GameManager.bulletManager.AwakeObject(0, point, out bl)) {
			bl.Shoot(this.branchBullet, BRANCH_SPEED, 0f, ref dir, 0f);
			// 本来はIDをキャッシュなり静的に定義するなりする
			bl.sortingLayer = SortingLayer.NameToID("BulletBranch");
			// わかりやすいよう大きめに設定する
			bl.CollisionRect(12f, 40f);
		} else {
			Debug.LogError("弾切れ : LINEAR");
		}
		// 奇数発と偶数発で逆旋回
		bullet.genericVector = (bullet.genericState == 0 ? BRANCH_ROT : BRANCH_ROT_INV) * bullet.genericVector;
		bullet.genericFloat[0] += BRANCH_SPAN;
	}
	#endregion
}
