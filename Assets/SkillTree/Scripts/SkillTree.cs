using System.Collections.Generic;
using UnityEngine;

public class SkillTree : MonoBehaviour
{
    public static SkillTree skillTree;
    private void Awake()
    {
        skillTree = this;
    }

    public GameObject skillTreeHolder;
    private List<Skill> skills;

    public enum CurrencyTypes
    {
        normal,
        special,
        super
    }

    public int normalCurrency;

    private void Start()
    {
        skills = new List<Skill>();
        foreach (Skill skill in skillTreeHolder.GetComponentsInChildren<Skill>()) skills.Add(skill);
    }
}
