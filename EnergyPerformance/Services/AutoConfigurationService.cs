﻿using EnergyPerformance.Helpers;
using EnergyPerformance.Models;

namespace EnergyPerformance.Services;
public class AutoConfigurationService
{
    private int _left;
    private int _right;
    private string _executable; // Application being auto-configured
    private List<float> _personaEnergyRates; // Enery rates in the persona customization slider
    private int stabilityCheckCount = 0; // The number of times the stability check has been inquired to the user.

    private const int delayInMinutes = 3; // Minutes till the next stability check

    public AutoConfigurationService()
    {
        // Create a list containing the Persona energy rates
        _personaEnergyRates = new List<float>();
        for (var i = 1.0f; i <= 3.0; i += 0.1f)
        {
            i = (float)Math.Round(i, 1);
            _personaEnergyRates.Add(i);
        }
    }

    public async void Start(string executable)
    {
        _executable = executable;

        _left = 0;
        _right = _personaEnergyRates.Count;

        var mid = _left + ((_right - _left) / 2);

        await App.GetService<PersonaModel>().UpdatePersona(_executable, _personaEnergyRates[mid]);
        await Task.Delay(TimeSpan.FromSeconds(5));

        PersonaNotification.StabilityCheckNotification(_executable, ++stabilityCheckCount);
    }


    /// <summary>
    /// Auto-configure the target application's Persona rate using Binary Search.
    /// </summary>
    /// <param name="isCurrentConfigurationStable">
    /// True for stable current configuration, False otherwise.
    /// </param>
    public async void AutoConfigure(bool isCurrentConfigurationStable)
    {
        if (_left < _right)
        {
            // Get the mid point
            var mid = _left + ((_right - _left) / 2);

            await App.GetService<PersonaModel>().UpdatePersona(_executable, _personaEnergyRates[mid]);

            // If the user indicates that the current configuration is stable,
            // then move towards a more performant energy rate
            // else move towards a more efficient energy rate
            if (isCurrentConfigurationStable)
            {
                _left = mid + 1;
            }
            else
            {
                _right = mid - 1;
            }
            await Task.Delay(TimeSpan.FromSeconds(5));
            PersonaNotification.StabilityCheckNotification(_executable, ++stabilityCheckCount);
        }
        else
        {
            // If the user indicates that the current configuration is stable,
            // then notify user that auto-configuration was success
            // else notify user that auto-configuration failed
            if (isCurrentConfigurationStable)
            {
                PersonaNotification.AutoConfigurationSuccessNotification(_executable);
            }
            else
            {
                PersonaNotification.FailedAutoConfigurationNotification(_executable);
            }
            stabilityCheckCount = 0;
        }
    }
}