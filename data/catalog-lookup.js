(function () {
  const CATALOG = window.CATALOG_VALUES || { years: [], makes: {} };

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

  window.initCatalogLookup = function initCatalogLookup() {
    const makeSelect = document.getElementById('catalogMake');
    const modelSelect = document.getElementById('catalogModel');
    const yearSelect = document.getElementById('catalogYear');
    if (!makeSelect || !modelSelect || !yearSelect) return;

    const makes = Object.keys(CATALOG.makes || {}).sort(sortText);
    fillSelect(makeSelect, makes, makes.length ? 'Select make...' : 'Catalog unavailable');
    resetSelect(modelSelect, 'Select model...');
    resetSelect(yearSelect, 'Select year...');
    modelSelect.disabled = true;
    yearSelect.disabled = true;
    setStatus(makes.length ? 'Select a catalog vehicle to copy its value into the insured value.' : 'Catalog data could not be loaded.');
  };

  window.onCatalogMakeChange = function onCatalogMakeChange() {
    const make = document.getElementById('catalogMake').value;
    const modelSelect = document.getElementById('catalogModel');
    const yearSelect = document.getElementById('catalogYear');
    document.getElementById('catalogValue').value = '';
    resetSelect(yearSelect, 'Select year...');
    yearSelect.disabled = true;

    if (!make || !CATALOG.makes[make]) {
      resetSelect(modelSelect, 'Select model...');
      modelSelect.disabled = true;
      setStatus('Select a catalog vehicle to copy its value into the insured value.');
      return;
    }

    const models = Object.keys(CATALOG.makes[make]).sort(sortText);
    fillSelect(modelSelect, models, 'Select model...');
    modelSelect.disabled = false;
    setStatus('Choose a model and year make.');
  };

  window.onCatalogModelChange = function onCatalogModelChange() {
    const make = document.getElementById('catalogMake').value;
    const model = document.getElementById('catalogModel').value;
    const yearSelect = document.getElementById('catalogYear');
    document.getElementById('catalogValue').value = '';

    if (!make || !model || !CATALOG.makes[make] || !CATALOG.makes[make][model]) {
      resetSelect(yearSelect, 'Select year...');
      yearSelect.disabled = true;
      setStatus('Choose a model and year make.');
      return;
    }

    const years = Object.keys(CATALOG.makes[make][model]).sort((a, b) => parseInt(b, 10) - parseInt(a, 10));
    fillSelect(yearSelect, years, 'Select year...');
    yearSelect.disabled = years.length === 0;
    setStatus(years.length ? 'Choose a year make to apply the catalog value.' : 'No catalog values found for this model.');
  };

  window.applyCatalogValue = function applyCatalogValue() {
    const make = document.getElementById('catalogMake').value;
    const model = document.getElementById('catalogModel').value;
    const year = document.getElementById('catalogYear').value;
    const value = CATALOG.makes && CATALOG.makes[make] && CATALOG.makes[make][model] ? CATALOG.makes[make][model][year] : null;
    const catalogValue = document.getElementById('catalogValue');

    if (!value) {
      catalogValue.value = '';
      setStatus('No catalog value found for this selection.');
      return;
    }

    const territory = window.currentTerritory || { code: 'CUR', currency: 'XCG' };
    const convertedValue = territory.code === 'BON' ? Number(value) / 1.8 : Number(value);
    const formattedValue = window.fmt ? fmt(convertedValue) : convertedValue.toFixed(2);
    const sourceNote = territory.code === 'BON' ? ' (XCG catalog value converted at 1.8)' :
      territory.code === 'ARU' ? ' (CUR catalog value used 1:1)' : '';

    catalogValue.value = formattedValue;
    document.getElementById('vehValue').value = convertedValue.toFixed(2);
    document.getElementById('vehYear').value = year;
    setStatus('Catalog value copied to insured value' + sourceNote + ': ', territory.currency + ' ' + formattedValue);

    if (!document.getElementById('results').classList.contains('hidden') && window.calculate) {
      calculate();
    }
  };

  window.addEventListener('load', window.initCatalogLookup);
}());
