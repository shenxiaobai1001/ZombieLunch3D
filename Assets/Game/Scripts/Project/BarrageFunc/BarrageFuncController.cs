using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BarrageFuncController : MonoBehaviour
{
    public static BarrageFuncController Instance;
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    private object _lockReadyFunc = new object(); // ������Ϊ readyFunc �����
    private object _lockExecutFunc = new object(); // ר������ executFunc ����

    [HideInInspector]
    public List<BarrageValue> readyFunc = new List<BarrageValue>();//�ȴ�ִ�еĹ��� 
    [HideInInspector]
    public Dictionary<int, BarrageValue> executFunc = new Dictionary<int, BarrageValue>();//����ִ�еĹ���  
    Dictionary<int, BarrageFuncData> allBarrage;

    bool hasBarrage = false;
    bool checkReadyFunck = false;
    int barrageIndex = 0;

    void Start()
    {
        GameData.Instance.Init();
        allBarrage = GameData.Instance.barrage_info.GetAllInfo();
        EventManager.Instance.AddListener(Events.OnBarryExecutEnd, OnBarryExecutEnd);
    }
    private void OnDestroy()
    {
        EventManager.Instance.RemoveListener(Events.OnBarryExecutEnd, OnBarryExecutEnd);
    }

    void OnBarryExecutEnd(object msg)
    {
        int index = (int)msg;
        lock (_lockExecutFunc)
        {
            if (executFunc != null && executFunc.Count > 0)
            {
                executFunc.Remove(index);
            }
        }
    }

    /// <summary> ������Ļ���� </summary>
    public void OnAddReadyFunc(ActionTask task)
    {
        if (readyFunc == null) readyFunc = new List<BarrageValue>();
        if (executFunc == null) executFunc = new Dictionary<int, BarrageValue>();

        var data = GameData.Instance.barrage_info.GetInfo(task.callName);
        PFunc.Log("\u89e6\u53d1\u5f39\u5e55\u529f\u80fd", task.callName, data);

        if (data == null)
        {
            Debug.LogWarning($"[Barrage] 配置缺失，跳过功能: {task.callName}（请检查 barrage.csv 名称是否一致）");
            return;
        }

        BarrageValue barrageValue = new BarrageValue
        {
            task = task,
            name = task.callName,
            barrageFuncData = data,
            BarrageState = BarrageState.Tigger,
            barraegExecutType = BarraegExecutType.ReadyExecut,
        };

        if ((OnCheckHighGroupFunc(data) || OnCheckHighLevelFunc(data) || OnCheckQueueLevel(data))
            && data.queuestate == 0)
        {
            OnAddReadFunc();
        }
        else
        {
            lock (_lockExecutFunc)
            {
                barrageIndex++;
                executFunc.Add(barrageIndex, barrageValue);
            }
        }

        if (!hasBarrage)
        {
            hasBarrage = true;
            StartCoroutine(OnExecutFunc());
        }

        void OnAddReadFunc()
        {
            lock (_lockReadyFunc)  // �޸ģ����ʱ����
            {
                readyFunc.Add(barrageValue);
            }
            if (!checkReadyFunck)
            {
                checkReadyFunck = true;
                StartCoroutine(OnCheckReadyFunc());
            }
        }
    }

    IEnumerator OnExecutFunc()
    {
        while (executFunc.Count > 0)
        {
            List<KeyValuePair<int, BarrageValue>> copy;
            lock (_lockExecutFunc)
            {
                // ���� executFunc �ĸ���
                copy = new List<KeyValuePair<int, BarrageValue>>(executFunc);
            }

            foreach (var kvp in copy)
            {
                if (kvp.Value.barraegExecutType == BarraegExecutType.ReadyExecut)
                {
                    BarrageExecutting.OnExecutingBarrage(kvp.Value.task, kvp.Key, kvp.Value);
                    lock (_lockExecutFunc)
                    {
                        // �޸�ֵǰȷ�����Դ���
                        if (executFunc.ContainsKey(kvp.Key))
                        {
                            executFunc[kvp.Key].barraegExecutType = BarraegExecutType.Executing;
                        }
                    }
                    yield return null;
                }
            }
            yield return new WaitForSeconds(0.1f);
        }
        hasBarrage = false;
    }
    IEnumerator OnCheckReadyFunc()
    {
        List<BarrageValue> temporaryFunc = new List<BarrageValue>();
        while (readyFunc.Count>0)
        {

            // �޸ģ������ڻ�ȡ readyFunc �ĸ���
            List<BarrageValue> copyReadyFunc;
            lock (_lockReadyFunc)
            {
                if (readyFunc.Count == 0)
                {
                    checkReadyFunck = false;
                    yield break;  // ���û������ֱ�ӽ���Э��
                }
                copyReadyFunc = new List<BarrageValue>(readyFunc);
            }

            // ��������������ԭ�б�
            foreach (var kvp in copyReadyFunc)
            {
                if (!OnCheckHighGroupFunc(kvp.barrageFuncData)//û��ͬ�����
                    && !OnCheckHighLevelFunc(kvp.barrageFuncData)//û��ͬ������
                    && !OnCheckQueueLevel(kvp.barrageFuncData))//û�������Ŷ�
                {
                    lock (_lockExecutFunc)
                    {
                        barrageIndex++;
                        executFunc.Add(barrageIndex, kvp);
                    }
                    temporaryFunc.Add(kvp);
                    if (!hasBarrage)
                    {
                        hasBarrage = true;
                        StartCoroutine(OnExecutFunc());
                    }
                    yield return new WaitForSeconds(0.1f);
                }
                yield return null;
            }

            // �� readyFunc ���Ƴ��Ѵ��������
            if (temporaryFunc.Count > 0)
            {
                lock (_lockReadyFunc)
                {
                    foreach (var kvp in temporaryFunc)
                    {
                        if (readyFunc.Contains(kvp))
                        {
                            readyFunc.Remove(kvp);
                        }
                    }
                }
                temporaryFunc.Clear();
            }

            yield return new WaitForSeconds(0.1f);
        }
        checkReadyFunck = false;
    }

    /// <summary> �����û��ͬ����ִ�еȼ����� </summary>
    public bool OnCheckHighGroupFunc(BarrageFuncData data)
    {
        if (data == null) return false;
        bool isHigh = false;
        if (data.group != 0)
        {
            List<KeyValuePair<int, BarrageValue>> copy;
            lock (_lockExecutFunc)
            {
                copy = new List<KeyValuePair<int, BarrageValue>>(executFunc);
            }
            foreach (var kvp in copy)
            {
                if (kvp.Value.barrageFuncData.group == data.group)
                {
                    isHigh = kvp.Value.barrageFuncData.executionlevel > data.executionlevel;
                    if (isHigh) break;
                }
            }
        }
        return isHigh;
    }

    /// <summary> �����û��ͬ����ִ�еȼ����� </summary>
    public bool OnCheckHighLevelFunc(BarrageFuncData data)
    {
        if (data == null) return false;
        bool isHigh = false;
        List<KeyValuePair<int, BarrageValue>> copy;
        lock (_lockExecutFunc)
        {
            copy = new List<KeyValuePair<int, BarrageValue>>(executFunc);
        }
        foreach (var kvp in copy)
        {
            if (kvp.Value.barrageFuncData.type != data.type) continue;
            switch (kvp.Value.barrageFuncData.type)
            {
                case 1:
                    isHigh = kvp.Value.barrageFuncData.createlevel > data.createlevel;
                    break;
                case 2:
                    isHigh = kvp.Value.barrageFuncData.movelevel > data.movelevel;
                    break;
                case 3:
                    isHigh = kvp.Value.barrageFuncData.controllevel > data.controllevel;
                    break;
                case 4:
                    isHigh = false;
                    break;
            }
            if (isHigh) break;
        }
        return isHigh;
    }
    /// <summary> �����û��ͬ�����Ŷ� </summary>
    public bool OnCheckQueueLevel(BarrageFuncData data)
    {
        if (data == null) return false;
        bool isHigh = false;
        if (data.queue == 1)
        {
            List<KeyValuePair<int, BarrageValue>> copy; 
            lock (_lockExecutFunc)
            {
                copy = new List<KeyValuePair<int, BarrageValue>>(executFunc);
            }
            foreach (var kvp in copy)
            {
                isHigh = kvp.Value.barrageFuncData.name == data.name;
                if (isHigh) break;
            }
        }
       // PFunc.Log("OnCheckQueueLevel", data.name, isHigh);
        return isHigh;
    }

    /// <summary> �����û��ǿ�����ϵĹ����ڴ��� </summary>
    public bool OnCheckHasHighControl(BarrageFuncData data)
    {
        bool isHigh = false;
        List<KeyValuePair<int, BarrageValue>> copy;
        lock (_lockExecutFunc)
        {
            copy = new List<KeyValuePair<int, BarrageValue>>(executFunc);
        }
        foreach (var kvp in copy)
        {
            isHigh = kvp.Value.name!= data.name && kvp.Value.barrageFuncData.type >= 3;
            if (isHigh) break;
        }
        return isHigh;
    }    
    /// <summary> �����û��ǿ�����ϵĹ����ڴ��� </summary>
    public bool OnCheckHasHighControl()
    {
        bool isHigh = false;
        List<KeyValuePair<int, BarrageValue>> copy;
        lock (_lockExecutFunc)
        {
            copy = new List<KeyValuePair<int, BarrageValue>>(executFunc);
        }
        foreach (var kvp in copy)
        {
            isHigh =  kvp.Value.barrageFuncData.type >= 3 && (kvp.Value.BarrageState == BarrageState.Underway
                || kvp.Value.BarrageState == BarrageState.Ready || kvp.Value.BarrageState == BarrageState.Pause);
           // PFunc.Log("���ǿ��", kvp.Value.barrageFuncData.name,kvp.Value.BarrageState);
            if (isHigh) break;
        }
        return isHigh;
    }

    /// <summary> �����û��ǿ���ҵȼ��������ϵĹ����ڴ��� </summary>
    public bool OnCheckHasHighControlLevel(BarrageFuncData data)
    {
        bool isHigh = false;
        List<KeyValuePair<int, BarrageValue>> copy;
        lock (_lockExecutFunc)
        {
            copy = new List<KeyValuePair<int, BarrageValue>>(executFunc);
        }
        foreach (var kvp in copy)
        {
            isHigh = kvp.Value.name != data.name 
                && kvp.Value.barrageFuncData.type >= 3 
                && kvp.Value.barrageFuncData.controllevel > data.controllevel;
                 if (isHigh) break;
        }

        return isHigh;
    }
    
    /// <summary> �����û��ĳ��������ִ�� </summary>
    public bool OnCheckBarrageFuncByName(string name)
    {
        bool isHigh = false;
        List<KeyValuePair<int, BarrageValue>> copy;
        lock (_lockExecutFunc)
        {
            copy = new List<KeyValuePair<int, BarrageValue>>(executFunc);
        }
        foreach (var kvp in copy)
        {
            isHigh = kvp.Value.name == name
                && (kvp.Value.BarrageState == BarrageState.Underway
                || kvp.Value.BarrageState == BarrageState.Pause);
            if (isHigh) break;
        }
        return isHigh;
    }
}

public class BarrageValue
{
    public ActionTask task;
    public string name;
    public BarrageFuncData barrageFuncData;
    public BarrageState BarrageState;
    public BarraegExecutType barraegExecutType;
}
