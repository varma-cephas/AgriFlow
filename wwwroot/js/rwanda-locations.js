const rwandaLocations = {
    "Eastern Province": {
        "Nyagatare": {
            "Rukomo": ["Gahurura", "Gashenyi", "Nyakagarama", "Rukomo II"],
            "Nyagatare": ["Barija", "Bushoga", "Kamagiri", "Nyagatare"],
            "Karangazi": ["Kamate", "Musenyi", "Ndama", "Rwenyanga"]
        },
        "Rwamagana": {
            "Kigabiro": ["Cyanya", "Nyagasenyi", "Sibagire"],
            "Muhazi": ["Kabare", "Kitazigurwa", "Murambi"]
        }
    },
    "Kigali City": {
        "Gasabo": {
            "Kimironko": ["Bibare", "Kibagabaga", "Nyagatovu"],
            "Remera": ["Nyabisindu", "Rukiri I", "Rukiri II"]
        },
        "Kicukiro": {
            "Niboye": ["Gatare", "Niboye", "Nyakabanda"]
        }
    },
    "Northern Province": {
        "Musanze": {
            "Muhoza": ["Cyabararika", "Kigombe", "Mpenge", "Ruhengeri"]
        }
    },
    "Southern Province": {
        "Huye": {
            "Ngoma": ["Butare", "Kaburemera", "Matyazo"]
        }
    },
    "Western Province": {
        "Rubavu": {
            "Gisenyi": ["Bugoyi", "Kivumu", "Nengo"]
        }
    }
};

document.addEventListener("DOMContentLoaded", () => {
    const provSel = document.getElementById("provinceSelect");
    const distSel = document.getElementById("districtSelect");
    const sectSel = document.getElementById("sectorSelect");
    const cellSel = document.getElementById("cellSelect");

    if (!provSel) return;

    const resetSelect = (el, label) => {
        el.innerHTML = `<option value="">${label}</option>`;
    };

    Object.keys(rwandaLocations).forEach(prov => {
        provSel.add(new Option(prov, prov));
    });

    provSel.addEventListener("change", () => {
        resetSelect(distSel, "Select district...");
        resetSelect(sectSel, "Select sector...");
        resetSelect(cellSel, "Select cell...");
        const districts = rwandaLocations[provSel.value];
        if (districts) {
            Object.keys(districts).forEach(d => distSel.add(new Option(d, d)));
        }
    });

    distSel.addEventListener("change", () => {
        resetSelect(sectSel, "Select sector...");
        resetSelect(cellSel, "Select cell...");
        const sectors = rwandaLocations[provSel.value]?.[distSel.value];
        if (sectors) {
            Object.keys(sectors).forEach(s => sectSel.add(new Option(s, s)));
        }
    });

    sectSel.addEventListener("change", () => {
        resetSelect(cellSel, "Select cell...");
        const cells = rwandaLocations[provSel.value]?.[distSel.value]?.[sectSel.value];
        if (cells) {
            cells.forEach(c => cellSel.add(new Option(c, c)));
        }
    });
});