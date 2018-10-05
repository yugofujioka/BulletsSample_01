using UnityEngine;
using TaskSystem;


// オブジェクトプール
public class ObjectPool<T> where T : CachedBehaviour {
    // 各オブジェクトの共有設定
    private struct ObjectParam {
        public Object prefab;       // 元のprefab
        public Transform root;      // 生成した親ノード
        public T[] pool;            // 空きオブジェクトバッファ
        public int freeIndex;       // 空きオブジェクトインデックス
        public int genMax;          // 生成限界数
        public int genCount;        // 生成した数
    }

    private int category = 0;       // オブジェクトのカテゴリ
    private ObjectParam objParams;  // オブジェクト情報
    private T[] objList = null;     // 全オブジェクトリスト
    private TaskSystem<T> activeObjTask = null;// 稼動オブジェクトタスク
    private int orderCount = 0;     // 実行オブジェクト数
    
    private float advanceTime = 0f; // procHandler経過時間
    // デリゲートキャッシュ
    private OrderHandler<T> procHandler = null;
    private OrderHandler<T> clearHandler = null;
    
    
    // 稼動数
    public int actCount { get { return this.activeObjTask.count; } }
    
    
    // コンストラクタ
    public ObjectPool() {
        // デリゲートキャッシュ
        // 複雑な処理でないので今回はラムダ式で手抜きしている
        this.procHandler = new OrderHandler<T>((obj, no) => {
            float elapsedTime = this.advanceTime;
            // 新規追加されたものは経過時間0sec.
            if (no >= this.orderCount)
                elapsedTime = 0f;
            
            if (!obj.Run(no, elapsedTime)) {
                this.Sleep(obj);
                return false;
            }
            return true;
        });
        this.clearHandler = new OrderHandler<T>((obj, no) => {
            this.Sleep(obj);
            return false;
        });
    }
    
    // 初期化
    // category : オブジェクトのカテゴリ指定
    // catalog : 複製オブジェクトカタログ
    public void Initialize(int category, GameObject prefab, int genCount) {
    
        this.category = category;
    
        // Prefab読込
        int capacity = genCount;
    
        this.objList = new T[genCount];
        this.objParams.pool = new T[genCount];
        this.objParams.freeIndex = -1;
    
        this.objParams.genMax = genCount;
        this.objParams.genCount = 0;
        this.objParams.prefab = prefab;
            
        // 親ノード作成
        GameObject typeGo = new GameObject(prefab.name);
        typeGo.isStatic = true;
        Transform typeRoot = typeGo.transform;
        this.objParams.root = typeRoot;
        // MEMO: シーン切替で自動で削除させない
        Object.DontDestroyOnLoad(typeGo);
        
        this.activeObjTask = new TaskSystem<T>(capacity);
    }
    
    // 終了
    public void Final() {
        int count = this.objList.Length;
        for (int index = 0; index < count; ++index) {
            if (this.objList[index] == null)
                break;
                    
            this.objList[index].Release();
        }
            
        Object.Destroy(this.objParams.root);
    
        this.category = 0;
        this.objList = null;
        this.activeObjTask = null;
    }
    
    // 全オブジェクトの生成
    // 生成数が増えると時間がかかりがちなのでInitializeと分ける
    public void Generate() {
        int genLimit = this.objParams.genMax -
                        this.objParams.genCount;
        for (int index = 0; index < genLimit; ++index) {
            if (this.objList[index] != null)
                continue;
    
            T obj = this.GenerateObject();
            int freeIndex = ++this.objParams.freeIndex;
            this.objParams.pool[freeIndex] = obj;
        }
    }
    
    // オブジェクトの生成
    // type : オブジェクトの種類
    private T GenerateObject() {
        int index = this.objParams.genCount;
        Object prefab = this.objParams.prefab;
        Transform root = this.objParams.root;
    
        GameObject go = Object.Instantiate(prefab, root) as GameObject;
#if UNITY_EDITOR
        go.name = string.Format(this.objParams.prefab.name + "{0:D2}", this.objParams.genCount);
#endif
        T obj = go.GetComponent<T>();
    
        // ユニークIDを割り振り
        obj.Create(UNIQUEID.Create(
            UNIQUEID.CATEGORYBIT(this.category) |
            UNIQUEID.TYPEBIT(0) |
            UNIQUEID.INDEXBIT(index)));
    
        this.objList[index] = obj;
        ++this.objParams.genCount;
    
        return obj;
    }
    
    // フレームの頭で呼ばれる処理
    public void FrameTop() {
        // 更新オブジェクト数の更新
        this.orderCount = this.activeObjTask.count;
    }
    // 定期更新
    // elapsedTime : 経過時間
    public void Proc(float elapsedTime) {
        this.advanceTime = elapsedTime;
        if (this.activeObjTask.count > 0) {
            this.activeObjTask.Order(this.procHandler);
            this.orderCount = this.activeObjTask.count;
        }
    }
    // 種類別有効数取得
    // type : 種類
    public int GetActiveCount(int type) {
        return this.objParams.genCount -
                (this.objParams.freeIndex + 1);
    }
    // 全消去
    public void Clear() {
        this.activeObjTask.Order(this.clearHandler);
    }
    
    // オブジェクト呼び出し
    // type : 種類
    // localPosition : 生成座標
    // obj : 生成したオブジェクト
    // return : 呼び出しに成功
    public bool AwakeObject(int type, Vector3 localPosition, out T obj) {
        if (this.PickOutObject(type, out obj)) {
            int no = this.activeObjTask.count - 1;
            obj.WakeUp(no, localPosition);
            return true;
        }
        return false;
    }
    // オブジェクト取得
    // unique : ユニークID
    // obj : 対象オブジェクト
    // return : IDが一致したか（異なる場合は既に一度回収されている）
    public bool GetObject(UNIQUEID unique, out T obj) {
        // 関係のないユニークID
        if (this.category != unique.category) {
            obj = null;
            return false;
        }
    
        obj = this.objList[unique.index];
        if (!obj.isAlive)
            return false;
    
        // フラッシュIDが更新されていれば別人
        return (obj.uniqueId == unique);
    }
    
    // オブジェクト取り出し
    // type : 種類
    // obj : 取り出したオブジェクト
    private bool PickOutObject(int type, out T obj) {
        obj = null;
    
        // 空きオブジェクトを取り出す
        if (this.objParams.freeIndex >= 0) {
            obj = this.objParams.pool[this.objParams.freeIndex];
            --this.objParams.freeIndex;
        } else {
            return false;
        }
    
        this.activeObjTask.Attach(obj);
        obj.uniqueId.Update();
    
        return true;
    }
    // 稼動終了処理
    // obj : オブジェクト
    private void Sleep(T obj) {
        int type = obj.uniqueId.type;
        ++this.objParams.freeIndex;
        this.objParams.pool[this.objParams.freeIndex] = obj;
        obj.Sleep();
    }
}