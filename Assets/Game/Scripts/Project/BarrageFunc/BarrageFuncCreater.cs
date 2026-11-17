using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.UI;

public class BarrageFuncCreater : MonoBehaviour
{
    public static BarrageFuncCreater Instance;
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

    public GameObject barrageQilinbi;
    public GameObject barrageTcjiao;
    public GameObject barrageShoeShine;

    public void OnCreateQLBi(BarrageValue barrageFuncData, int index)
    => InstantiateBarrageFunc(barrageQilinbi, barrageFuncData, index, barrageFuncData.name);

    public void OnCreateTCJiao(BarrageValue barrageFuncData, int index)
    => InstantiateBarrageFunc(barrageTcjiao, barrageFuncData, index, barrageFuncData.name);
    public void OnCreateShoe(BarrageValue barrageFuncData, int index)
    {
        Sound.PlaySound("Mod/capixie");
        if (BarrageFuncController.Instance.OnCheckBarrageFuncByName("²ÁÆ¤Ð¬"))
        {
            ModData.shineCount += 5;
            EventManager.Instance.SendMessage(Events.OnBarryExecutEnd, index);
        }
        else
        {
            InstantiateBarrageFunc(barrageShoeShine, barrageFuncData, index, barrageFuncData.name);
        }
    }
    public GameObject InstantiateBarrageFunc(GameObject prefab, BarrageValue barrageFuncData, int index, string callName = "")
    {
        Vector3 createPos = OnCreatePos(callName);
        PFunc.Log(createPos, callName);
        GameObject obj = SimplePool.Spawn(prefab, createPos, Quaternion.identity);
        obj.transform.SetParent(transform);
        obj.SetActive(true);

        OnCreateEnd(barrageFuncData.name, obj, barrageFuncData, index);
        return obj;
    }

    public Vector3 OnCreatePos(string callName)
    {
        Vector3 createPos = Vector3.zero;
        if (string.IsNullOrEmpty(callName)) return createPos;
        //Vector3 vectorPlayer = PlayerController.Instance.transform.position;
        //switch (callName)
        //{
        //    case "²ÁÆ¤Ð¬":
        //        createPos = vectorPlayer;
        //        break;

        //}
        return createPos;
    }

    public void OnCreateEnd(string callName, GameObject obj, BarrageValue barrageFuncData, int index)
    {
        //switch (callName)
        //{
        //    case "÷è÷ë±Û":
        //        Sound.PlaySound("Mod/QLBi");
        //        obj.GetComponent<QLBI>().OnStart(barrageFuncData, index);
        //        break;
        //    case "Ìì²Ð½Å":
        //        Sound.PlaySound("Mod/TCJiao");
        //        obj.GetComponent<TCJiao>().OnStart(barrageFuncData, index);
        //        break;
        //    case "²ÁÆ¤Ð¬":
        //        ModData.shineCount += 5;
        //        obj.GetComponent<BarrageShoeShine>().OnStart(barrageFuncData, index);
        //        break;
        //}
    }
}
