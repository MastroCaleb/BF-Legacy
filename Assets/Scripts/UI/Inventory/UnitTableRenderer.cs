using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UnitTableRenderer : MonoBehaviour
{
    public Unit unitData;
    public UnitInventoryData inventoryData;

    public GameObject convCircleSam, convCircleAddSam;

    public BraveFrontierFrameAnimator animatorCGG;
    public SamAnimator animatorSAM;
    
    void Start()
    {
        if(unitData != null)
        {
            ComposeUnit();
        }
    }
    
    public void SetUnit(UnitInventoryData inventoryData, bool isBaseUnit)
    {
        this.inventoryData = inventoryData;
        this.unitData = inventoryData.unit;
        if (convCircleSam != null && convCircleAddSam != null)
        {
            convCircleSam.SetActive(isBaseUnit);
            convCircleAddSam.SetActive(isBaseUnit);
        }
        ComposeUnit();
    }

    public void SetUnit(Unit unitData, bool isBaseUnit)
    {
        this.unitData = unitData;
        if (convCircleSam != null && convCircleAddSam != null)
        {
            convCircleSam.SetActive(isBaseUnit);
            convCircleAddSam.SetActive(isBaseUnit);
        }
        ComposeUnit();
    }

    public void ClearUnit()
    {
        this.inventoryData = null;
        this.unitData = null;
        GetComponent<Image>().enabled = false;
        GetComponent<BraveFrontierFrameAnimator>().enabled = false;
        GetComponent<SamAnimator>().enabled = false;
        
        if (convCircleSam != null && convCircleAddSam != null)
        {
            convCircleSam.SetActive(false);
            convCircleAddSam.SetActive(false);
        }
    }

    public void ComposeUnit()
    {
        if(unitData == null)
        {
            GetComponent<Image>().enabled = false;
            GetComponent<SamAnimator>().jsonFile = null;
            GetComponent<SamAnimator>().Reinitialize();
            return;
        }

        if (unitData.animationType == AnimationType.CGG)
        {
            GetComponent<SamAnimator>().enabled = false;
            GetComponent<SamAnimator>().jsonFile = null;
            GetComponent<SamAnimator>().Reinitialize();
            GetComponent<BraveFrontierFrameAnimator>().enabled = true;
            GetComponent<Image>().enabled = true;
            animatorCGG = GetComponent<BraveFrontierFrameAnimator>();
            animatorCGG.spriteSheets  = unitData.spriteSheets;
            animatorCGG.cggFile       = unitData.cggFile;
            animatorCGG.idleCgsFile   = unitData.idleCgsFile;
            animatorCGG.moveCgsFile   = unitData.moveCgsFile;
            animatorCGG.attackCgsFile = unitData.attackCgsFile;
            animatorCGG.InitializeAnimator();
        }
        else
        {
            GetComponent<BraveFrontierFrameAnimator>().enabled = false;
            GetComponent<SamAnimator>().enabled = true;
            GetComponent<Image>().enabled = false;
            animatorSAM          = GetComponent<SamAnimator>();
            animatorSAM.enabled  = true;
            animatorSAM.jsonFile = unitData.samFile;
            animatorSAM.Reinitialize();
        }

        FlipImage(false);
    }

    public void FlipImage(bool flipX)
    {
        float size = 1.75f;
        Image sr = GetComponent<Image>();
        sr.rectTransform.localScale = new Vector3(flipX ? -size :  size, size, size);
    }
}
