(function () {
  const EMPTY_CATALOG = { years: [], makes: {} };
  const OTHER_MAKE = '__OTHER__';
  const CUSTOM_STORAGE_KEY = 'nagicoCustomCatalogValues';

  function getTerritory() {
    return window.currentTerritory || { code: 'CUR', currency: 'XCG' };
  }

  function getBaseCatalog() {
    const territory = getTerritory();
    if (territory.code === 'ARU' && window.ARU_CATALOG_VALUES) {
      return window.ARU_CATALOG_VALUES;
    }
    return window.CATALOG_VALUES || EMPTY_CATALOG;
  }

  function loadCustomCatalogs() {
    try {
      return JSON.parse(localStorage.getItem(CUSTOM_STORAGE_KEY) || '{}') || {};
    } catch (err) {
      return {};
    }
  }

  function saveCustomCatalogs(data) {
    try {
      localStorage.setItem(CUSTOM_STORAGE_KEY, JSON.stringify(data));
      return true;
    } catch (err) {
      return false;
    }
  }

  function getTerritoryCustomCatalog(code) {
    const all = loadCustomCatalogs();
    return all[code] || EMPTY_CATALOG;
  }

  function cloneCatalog(catalog) {
    const clone = { years: [...(catalog.years || [])], makes: {} };
    Object.entries(catalog.makes || {}).forEach(([make, models]) => {
      clone.makes[make] = {};
      Object.entries(models || {}).forEach(([model, years]) => {
        clone.makes[make][model] = { ...(years || {}) };
      });
    });
    return clone;
  }

  function getActiveCatalog() {
    const territory = getTerritory();
    const merged = cloneCatalog(getBaseCatalog());
    const custom = getTerritoryCustomCatalog(territory.code);

    Object.entries(custom.makes || {}).forEach(([make, models]) => {
      if (!merged.makes[make]) merged.makes[make] = {};
      Object.entries(models || {}).forEach(([model, years]) => {
        if (!merged.makes[make][model]) merged.makes[make][model] = {};
        Object.assign(merged.makes[make][model], years || {});
      });
    });

    merged.years = [...new Set(Object.keys(merged.makes).flatMap(make =>
      Object.values(merged.makes[make] || {}).flatMap(years => Object.keys(years || {}))
    ))].sort((a, b) => parseInt(b, 10) - parseInt(a, 10));

    return merged;
  }

  function isCustomCatalogEntry(make, model, year) {
    const territory = getTerritory();
    const custom = getTerritoryCustomCatalog(territory.code);
    return Boolean(custom.makes && custom.makes[make] && custom.makes[make][model] && custom.makes[make][model][year]);
  }

  function sortText(a, b) {
    return a.localeCompare(b, undefined, { numeric: true, sensitivity: 'base' });
  }

  function resetSelect(select, placeholder) {
    select.innerHTML = '';
    const option = document.createElement('option');
    option.value = '';
    option.textContent = placeholder;
    select.appendChild(option);
    select.value = '';
  }

  function fillSelect(select, values, placeholder) {
    resetSelect(select, placeholder);
    values.forEach(value => {
      const option = document.createElement('option');
      option.value = value;
      option.textContent = value;
      select.appendChild(option);
    });
  }

  function addOtherOption(select) {
    const option = document.createElement('option');
    option.value = OTHER_MAKE;
    option.textContent = 'Other...';
    const insertBefore = select.options.length > 1 ? select.options[1] : null;
    select.insertBefore(option, insertBefore);
  }

  function setManualFieldsVisible(visible) {
    const container = document.getElementById('customCatalogFields');
    if (container) container.classList.toggle('hidden', !visible);
  }

  function setStatus(message, value) {
    const status = document.getElementById('catalogStatus');
    if (!status) return;
    status.textContent = message || '';
    if (value) {
      const strong = document.createElement('strong');
      strong.textContent = value;
      status.appendChild(strong);
    }
  }

  function setCatalogSelection(make, model, year) {
    const makeSelect = document.getElementById('catalogMake');
    const modelSelect = document.getElementById('catalogModel');
    const yearSelect = document.getElementById('catalogYear');
    if (!makeSelect || !modelSelect || !yearSelect) return;

    makeSelect.value = make;
    window.onCatalogMakeChange();
    modelSelect.value = model;
    window.onCatalogModelChange();
    yearSelect.value = String(year);
    window.applyCatalogValue();
  }
  function getSelectedModelYears(catalog, make, model) {
    const years = catalog.makes && catalog.makes[make] && catalog.makes[make][model]
      ? Object.keys(catalog.makes[make][model])
      : [];
    return years.map(year => parseInt(year, 10)).filter(year => Number.isFinite(year));
  }

  function setCustomInputValue(id, value) {
    const input = document.getElementById(id);
    if (input) input.value = value || '';
  }

  function focusCustomValue() {
    const input = document.getElementById('customCatalogValue') || document.getElementById('customCatalogYear');
    if (input) input.focus();
  }

  window.startCatalogNewYearEntry = function startCatalogNewYearEntry() {
    const catalog = getActiveCatalog();
    const makeSelect = document.getElementById('catalogMake');
    const modelSelect = document.getElementById('catalogModel');
    const yearSelect = document.getElementById('catalogYear');
    const make = makeSelect?.value || '';
    const model = modelSelect?.value || '';

    if (make === OTHER_MAKE) {
      setCustomInputValue('customCatalogMake', '');
      setCustomInputValue('customCatalogModel', '');
      setCustomInputValue('customCatalogYear', '');
      setCustomInputValue('customCatalogValue', '');
      setManualFieldsVisible(true);
      setStatus('Enter the new vehicle details and save the catalog entry.');
      focusCustomValue();
      return;
    }

    if (!make || !catalog.makes[make]) {
      setStatus('Select a catalog make first, or choose Other... to add a new vehicle.');
      return;
    }

    if (!model || !catalog.makes[make][model]) {
      setStatus('Select a catalog model first, then add the new year and value.');
      return;
    }

    const existingYears = getSelectedModelYears(catalog, make, model);
    const selectedYear = parseInt(yearSelect?.value || '', 10);
    const latestYear = Number.isFinite(selectedYear) ? selectedYear : Math.max(...existingYears);
    const nextYear = Number.isFinite(latestYear) ? latestYear + 1 : new Date().getFullYear();

    setCustomInputValue('customCatalogMake', make);
    setCustomInputValue('customCatalogModel', model);
    setCustomInputValue('customCatalogYear', String(nextYear));
    setCustomInputValue('customCatalogValue', '');
    setManualFieldsVisible(true);
    setStatus('Adding a new catalog year for ' + make + ' ' + model + '. Enter the catalog value and save.');
    focusCustomValue();
  };

  window.initCatalogLookup = function initCatalogLookup() {
    const makeSelect = document.getElementById('catalogMake');
    const modelSelect = document.getElementById('catalogModel');
    const yearSelect = document.getElementById('catalogYear');
    if (!makeSelect || !modelSelect || !yearSelect) return;

    const catalog = getActiveCatalog();
    const makes = Object.keys(catalog.makes || {}).sort(sortText);
    fillSelect(makeSelect, makes, makes.length ? 'Select make...' : 'Catalog unavailable');
    addOtherOption(makeSelect);
    resetSelect(modelSelect, 'Select model...');
    resetSelect(yearSelect, 'Select year...');
    modelSelect.disabled = true;
    yearSelect.disabled = true;
    document.getElementById('catalogValue').value = '';
    setManualFieldsVisible(false);
    setStatus(makes.length ? 'Select a catalog vehicle to copy its value into the insured value.' : 'Catalog data could not be loaded. Use Other to add a catalog entry.');
  };

  window.onCatalogMakeChange = function onCatalogMakeChange() {
    const catalog = getActiveCatalog();
    const make = document.getElementById('catalogMake').value;
    const modelSelect = document.getElementById('catalogModel');
    const yearSelect = document.getElementById('catalogYear');
    document.getElementById('catalogValue').value = '';
    resetSelect(yearSelect, 'Select year...');
    yearSelect.disabled = true;

    if (make === OTHER_MAKE) {
      resetSelect(modelSelect, 'Save entry first...');
      modelSelect.disabled = true;
      setManualFieldsVisible(true);
      setStatus('Enter the vehicle details and save the catalog entry.');
      return;
    }

    setManualFieldsVisible(false);

    if (!make || !catalog.makes[make]) {
      resetSelect(modelSelect, 'Select model...');
      modelSelect.disabled = true;
      setStatus('Select a catalog vehicle to copy its value into the insured value.');
      return;
    }

    const models = Object.keys(catalog.makes[make]).sort(sortText);
    fillSelect(modelSelect, models, 'Select model...');
    modelSelect.disabled = false;
    setStatus('Choose a model and year make.');
  };

  window.onCatalogModelChange = function onCatalogModelChange() {
    const catalog = getActiveCatalog();
    const make = document.getElementById('catalogMake').value;
    const model = document.getElementById('catalogModel').value;
    const yearSelect = document.getElementById('catalogYear');
    document.getElementById('catalogValue').value = '';

    if (!make || !model || !catalog.makes[make] || !catalog.makes[make][model]) {
      resetSelect(yearSelect, 'Select year...');
      yearSelect.disabled = true;
      setStatus('Choose a model and year make.');
      return;
    }

    const years = Object.keys(catalog.makes[make][model]).sort((a, b) => parseInt(b, 10) - parseInt(a, 10));
    fillSelect(yearSelect, years, 'Select year...');
    yearSelect.disabled = years.length === 0;
    setStatus(years.length ? 'Choose a year make to apply the catalog value.' : 'No catalog values found for this model.');
  };

  window.applyCatalogValue = function applyCatalogValue() {
    const catalog = getActiveCatalog();
    const make = document.getElementById('catalogMake').value;
    const model = document.getElementById('catalogModel').value;
    const year = document.getElementById('catalogYear').value;
    const value = catalog.makes && catalog.makes[make] && catalog.makes[make][model] ? catalog.makes[make][model][year] : null;
    const catalogValue = document.getElementById('catalogValue');

    if (!value) {
      catalogValue.value = '';
      setStatus('No catalog value found for this selection.');
      return;
    }

    const territory = getTerritory();
    const customEntry = isCustomCatalogEntry(make, model, year);
    const convertedValue = territory.code === 'BON' && !customEntry ? Number(value) / 1.8 : Number(value);
    const formattedValue = window.fmt ? window.fmt(convertedValue) : convertedValue.toFixed(2);
    const sourceNote = customEntry ? ' (saved catalog entry)' :
      territory.code === 'BON' ? ' (XCG catalog value converted at 1.8)' :
      territory.code === 'ARU' ? ' (ARU catalog value used)' : '';

    catalogValue.value = formattedValue;
    document.getElementById('vehValue').value = convertedValue.toFixed(2);
    document.getElementById('vehYear').value = year;
    setStatus('Catalog value copied to insured value' + sourceNote + ': ', territory.currency + ' ' + formattedValue);

    if (!document.getElementById('results').classList.contains('hidden') && window.calculate) {
      calculate();
    }
  };

  window.saveCustomCatalogEntry = function saveCustomCatalogEntry() {
    const territory = getTerritory();
    const make = (document.getElementById('customCatalogMake')?.value || '').trim();
    const model = (document.getElementById('customCatalogModel')?.value || '').trim();
    const year = String(parseInt(document.getElementById('customCatalogYear')?.value || '', 10));
    const value = Number(document.getElementById('customCatalogValue')?.value || 0);

    if (!make || !model || year === 'NaN' || Number(year) < 1900 || !Number.isFinite(value) || value <= 0) {
      setStatus('Enter a make, model, valid year, and catalog value before saving.');
      return;
    }

    const all = loadCustomCatalogs();
    if (!all[territory.code]) all[territory.code] = { years: [], makes: {} };
    const catalog = all[territory.code];
    if (!catalog.makes) catalog.makes = {};
    if (!catalog.years) catalog.years = [];
    if (!catalog.makes[make]) catalog.makes[make] = {};
    if (!catalog.makes[make][model]) catalog.makes[make][model] = {};
    catalog.makes[make][model][year] = Number(value.toFixed(2));
    catalog.years = [...new Set([...catalog.years, year])].sort((a, b) => parseInt(b, 10) - parseInt(a, 10));

    if (!saveCustomCatalogs(all)) {
      setStatus('The catalog entry could not be saved in this browser.');
      return;
    }

    window.initCatalogLookup();
    setCatalogSelection(make, model, year);
  };

  window.getCatalogPrintSelection = function getCatalogPrintSelection() {
    const make = document.getElementById('catalogMake')?.value || '';
    const model = document.getElementById('catalogModel')?.value || '';
    const year = document.getElementById('catalogYear')?.value || '';

    if (make && make !== OTHER_MAKE) {
      return { make, model, year };
    }

    return {
      make: (document.getElementById('customCatalogMake')?.value || '').trim(),
      model: (document.getElementById('customCatalogModel')?.value || '').trim(),
      year: (document.getElementById('customCatalogYear')?.value || '').trim()
    };
  };

  window.addEventListener('load', window.initCatalogLookup);
}());