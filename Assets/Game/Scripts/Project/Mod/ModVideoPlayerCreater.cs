using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;
using UnityEngine.Video;

public class ModVideoPlayerCreater : MonoBehaviour
{
    public static ModVideoPlayerCreater Instance;
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

    public GameObject ModVideoPlayer;
    public Transform videoParent;
    public bool IsPlaying = false; 
    public GameObject InteractableObjects;

    // 当前播放的视频编号和协程引用

    private int currentVideoNumber;
    private int grilVideoNumber;
    private Coroutine beatCoroutine;
    private void Start()
    {
        EventManager.Instance.AddListener(Events.OnModVideoPlayEnd, OnVideoPlayEnd);
        EventManager.Instance.AddListener(Events.OnModVideoPlayStart, OnModVideoPlayStart);
    }


    // 修改现有的视频播放结束方法
    void OnModVideoPlayStart(object msg)
    {
        
    }
    // 修改现有的视频播放结束方法
    void OnVideoPlayEnd(object msg)
    {
        IsPlaying = false;
        // 停止卡点协程
        if (beatCoroutine != null)
        {
            StopCoroutine(beatCoroutine);
            beatCoroutine = null;
        }
    }

    public GameObject OnCreateModVideoPlayer(Vector3 offset, Vector3 scale, Vector3 rotateA,string path, int type, UnityAction callback = null, string layer = "Video", bool snake = false,int sortingOrder=-5)
    {
        GameObject vplayerObj = SimplePool.Spawn(ModVideoPlayer, transform.position, Quaternion.identity);
        ModVideoPlayer vplayer = vplayerObj.GetComponent<ModVideoPlayer>();
        vplayerObj.SetActive(true);
        vplayer.OnPlayVideo(offset, scale, rotateA, path, type, layer, snake, sortingOrder, callback);
        vplayerObj.transform.SetParent(videoParent);
        IsPlaying = true;
        return vplayerObj;
    }

    private void OnDestroy()
    {
        EventManager.Instance.RemoveListener(Events.OnModVideoPlayEnd, OnVideoPlayEnd);
    }
}
