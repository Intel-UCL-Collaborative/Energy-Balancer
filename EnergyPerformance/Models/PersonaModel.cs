﻿using EnergyPerformance.Core.Contracts.Services;
using EnergyPerformance.Helpers;
using EnergyPerformance.Services;
using System.Diagnostics;
using System.Management;

namespace EnergyPerformance.Models;
public class PersonaModel
{
    private readonly PersonaFileService _personaFileService;
    private readonly ProcessMonitorService _processMonitorService;
    private readonly CpuInfo _cpuInfo;

    /// <summary>
    /// This field stores all existing personas in a list
    /// </summary>
    private List<PersonaEntry> _allPersonas;
    private int _nextPersonaId = 0;

    /// <summary>
    /// This property indicates whether any persona is enabled right now
    /// </summary>
    public bool IsEnabled
    {
        set; get;
    }

    /// <summary>
    /// This property points to the currently enabled persona
    /// </summary>
    public PersonaEntry? PersonaEnabled
    {
        set; get;
    }

    // private DebugModel Debug => App.GetService<DebugModel>();

    public PersonaModel(CpuInfo cpuInfo, PersonaFileService personaFileService)
    {
        _personaFileService = personaFileService;
        _allPersonas = new List<PersonaEntry>();
        _processMonitorService = new ProcessMonitorService();
        _cpuInfo = cpuInfo;
        IsEnabled = false;
        PersonaEnabled = null;
        foreach (var persona in _allPersonas)
            if (persona.Id > _nextPersonaId)
                _nextPersonaId = persona.Id + 1;

        _processMonitorService.CreationEventHandler += OnProcCreation;
        _processMonitorService.DeletionEventHandler += OnProcDeletion;
    }

    /// <summary>
    /// This method will be executed in ActivationService, and will read persona data from persistent storage
    /// </summary>
    /// <returns></returns>
    public async Task InitializeAsync()
    {
        _allPersonas = await _personaFileService.ReadFileAsync();
        foreach (var persona in _allPersonas)
            _processMonitorService.AddWatcher(persona.Path);

        _processMonitorService.AddWatcher("Gees.exe");
    }

    public void OnProcCreation(object sender, EventArgs e) 
    {
        Debug.WriteLine("Handling creation event");
        var proc = _processMonitorService.CreatedProcess;
        if (!IsEnabled && proc != null)
        {
            Debug.WriteLine(proc);
            EnablePersona(proc);
        }
    }

    public void OnProcDeletion(object sender, EventArgs e)
    {
        Debug.WriteLine("Handling deletion event");
    }

    /// <summary>
    /// This methods creates a new persona file based on the path and energy rating the user sets
    /// It converts the energy rating into respective CPU and GPU settings based on the hardware spec
    /// It assigns every persona a unique ID (this can be swapped with the unique ID returned by the database)
    /// </summary>
    /// <param name="path">The path to the executable of the program</param>
    /// <param name="energyRating">The position of the slider defined by the user</param>
    public async Task CreatePersona(string path, float energyRating)
    {
        PersonaEntry entry = new PersonaEntry(_nextPersonaId, path, ConvertRatingToCpuSetting(energyRating), ConvertRatingToGpuSetting(energyRating));
        _allPersonas.Add(entry);

        // Add creation and deletion watchers for the application
        _processMonitorService.AddWatcher(path);

        await _personaFileService.SaveFileAsync();
        _nextPersonaId += 1;
    }

    /// <summary>
    /// This method reads all existing personas
    /// And translates the raw CPU, GPU settings into the positioning of the slider
    /// </summary>
    /// <returns>A path name, energy rating pair for every application</returns>
    public List<(string, float)> ReadPersonaAndRating()
    {
        List<(string, float)> profiles = _allPersonas.Select(persona => (persona.Path, ConvertSettingsToRating(persona.CpuSetting, persona.GpuSetting))).ToList();
        return profiles;
    }

    /// <summary>
    /// This method is invoked when the user updates the setting on the frontend
    /// </summary>
    /// <param name="personaId">The unique ID of the persona</param>
    /// <param name="path">The path to the executable of the program</param>
    /// <param name="energyRating">User-defined slider position</param>
    public async Task UpdatePersona(int personaId, string path, float energyRating)
    {
        _allPersonas.ForEach(persona =>
        {
            if (persona.Id == personaId)
            {
                persona.Path = path;
                persona.CpuSetting = ConvertRatingToCpuSetting(energyRating);
                persona.GpuSetting = ConvertRatingToGpuSetting(energyRating);

                _processMonitorService.RemoveWatcher(path);
                _processMonitorService.AddWatcher(path);
            }
        });
        await _personaFileService.SaveFileAsync();
    }

    /// <summary>
    /// Removes the persona from the volatile and persistent memory
    /// </summary>
    /// <param name="personaId">The unique ID of the persona</param>
    public async Task DeletePersona(string personaName)
    {
        _processMonitorService.RemoveWatcher(personaName);
        var res = _allPersonas.RemoveAll(persona => persona.Path == personaName) > 0 ? true : false;
        await _personaFileService.SaveFileAsync();
    }

    /// <summary>
    /// Invoked when a program with a program that has a defined persona in EnergyGuard is launched
    /// Applies the CPU/GPU settings that correspond to the process to the hardware
    /// </summary>
    /// <param name="personaName">The name of the persona</param>
    /// <returns>Whether the enabling was successful</returns>
    public bool EnablePersona(string personaName)
    {
        var index = _allPersonas.FindIndex(persona => persona.Path == personaName);
        if (!IsEnabled && index != -1)
        {
            var persona = _allPersonas[index];
            // we need to research how to verify if a process is currently running
            // if it is not running, then the following lines should not be executed
            _cpuInfo.EnableCpuSetting(persona.Path, persona.CpuSetting);
            EnableGpuSetting(persona.GpuSetting);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Invoked when the user exits the selected program
    /// Disable the engaged persona setting
    /// </summary>
    /// <param name="personaName"></param>
    /// <returns></returns>
    public bool DisablePersona(string personaName)
    {
        var index = _allPersonas.FindIndex(persona => persona.Path == personaName);
        if (IsEnabled && index != -1)
        {
            var persona = _allPersonas[index];
            _cpuInfo.DisableCpuSetting(persona.Path, persona.CpuSetting);
            DisableGpuSetting(persona.GpuSetting);
            return true;
        }
        return false;
    }

    // we might want to move these methods into their own classes
    private void EnableGpuSetting(int gpuSetting)
    {
    }

    private void DisableGpuSetting(int gpuSetting)
    {
    }

    private int ConvertRatingToCpuSetting(float energyRating)
    {
        return 0;
    }
    private int ConvertRatingToGpuSetting(float energyRating)
    {
        return 0;
    }

    private float ConvertSettingsToRating(int cpuSetting, int gpuSetting)
    {
        return (float)0.0;
    }
}
