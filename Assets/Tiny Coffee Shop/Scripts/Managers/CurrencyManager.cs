using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Tabsil.Sijil;
using TMPro;
using System;

public class CurrencyManager : MonoBehaviour, IWantToBeSaved
{
    public static CurrencyManager instance;

    [Header(" Settings ")]
    private bool shouldSave;

    public int Currency { get; private set; }

    private const string currencyKey = "currency";

    [Header(" Actions ")]
    public static Action updated;

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(gameObject);

        InvokeRepeating("SaveIfNeeded", 0, 1);
    }

    public void Load()
    {
        if (Sijil.TryLoad(this, currencyKey, out object _currency))
            Currency = (int)_currency;
        else
            Currency = 525;

        UpdateTexts();
    }

    [NaughtyAttributes.Button]
    public void Add500Currency()
    {
        AddCurrency(500);    
    }

    public void AddCurrency(int amount)
    {
        Currency += amount;
        SaveAndUpdateVisuals();
    }

    private void SaveAndUpdateVisuals()
    {
        shouldSave = true;
        UpdateTexts();
        updated?.Invoke();
    }

    public void UseCurrency(int amount) => AddCurrency(-amount);

    public bool HasEnoughCurrency(int amount) => Currency >= amount;
    public bool HasEnoughPremiumCurrency(int amount) => Currency >= amount;

    public bool TryPurchase(int price)
    {
        if(HasEnoughCurrency(price))
        {
            AddCurrency(-price);
            return true;
        }

        return false;
    }

    private void UpdateTexts()
    {
        // We can cache these if we want more performance
        CurrencyText[] currencyTexts = 
            FindObjectsByType<CurrencyText>(FindObjectsInactive.Include, FindObjectsSortMode.None);
   
        foreach (CurrencyText ct in currencyTexts)
            ct.GetComponent<TextMeshProUGUI>().text = Currency.ToString();
    }

    private void SaveIfNeeded()
    {
        if (!shouldSave)
            return;

        shouldSave = false;
        Save();
    }

    public void Save() => Sijil.Save(this, currencyKey, Currency);
}
