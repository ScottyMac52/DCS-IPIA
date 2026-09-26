local diff = {
  ["keyDiffs"] = {
    ["d3003pnilunilcd1vd0vpnilvunil"] = {
      ["added"] = {
        [1] = { ["key"] = "JOY_BTN2" },
      },
      ["name"] = "Battery, off",
    },
    ["d3003pnilunilcd1vd1vpnilvunil"] = {
      ["added"] = {
        [1] = { ["key"] = "JOY_BTN1" },
      },
      ["name"] = "Battery, on",
    },
    ["d3208pnilunilcd3vd-0.333vpnilvunil"] = {
      ["added"] = {
        [1] = { ["key"] = "JOY_BTN4", ["reformers"] = { [1] = "JOY_BTN7" } },
      },
      ["name"] = "Ignition switch (magnets) -",
    },
    ["d3208pnilunilcd3vd0.333vpnilvunil"] = {
      ["added"] = {
        [1] = { ["key"] = "JOY_BTN3", ["reformers"] = { [1] = "JOY_BTN7" } },
      },
      ["name"] = "Ignition switch (magnets) +",
    },
    ["d3228pnilunilcd3vd0vpnilvunil"] = {
      ["added"] = {
        [1] = { ["key"] = "JOY_BTN4" },
      },
      ["name"] = "Fuel pump, off",
    },
    ["d3228pnilunilcd3vd1vpnilvunil"] = {
      ["added"] = {
        [1] = { ["key"] = "JOY_BTN3" },
      },
      ["name"] = "Fuel pump, on",
    },
    ["d3244pnilunilcd3vd0vpnilvunil"] = {
      ["added"] = {
        [1] = { ["key"] = "JOY_BTN6" },
      },
      ["name"] = "Disable water injection",
    },
    ["d3244pnilunilcd3vd1vpnilvunil"] = {
      ["added"] = {
        [1] = { ["key"] = "JOY_BTN5" },
      },
      ["name"] = "Enable water injection",
    },
    ["d3365pnilunilcd5vd0vpnilvunil"] = {
      ["added"] = {
        [1] = { ["key"] = "JOY_BTN2", ["reformers"] = { [1] = "JOY_BTN7" } },
      },
      ["name"] = "Directional gyro uncage",
    },
    ["d3365pnilunilcd5vd1vpnilvunil"] = {
      ["added"] = {
        [1] = { ["key"] = "JOY_BTN1", ["reformers"] = { [1] = "JOY_BTN7" } },
      },
      ["name"] = "Directional gyro cage",
    },
    ["d3932pnilunilcd13vd0vpnilvunil"] = {
      ["added"] = {
        [1] = { ["key"] = "JOY_BTN6", ["reformers"] = { [1] = "JOY_BTN7" } },
      },
      ["name"] = "Droppable tank lock: release",
    },
  },
  ["axisDiffs"] = {
    ["a2001cdnil"] = {
      ["removed"] = {
        [1] = { ["key"] = "JOY_Y" },
      },
      ["name"] = "Pitch",
    },
    ["a2002cdnil"] = {
      ["removed"] = {
        [1] = { ["key"] = "JOY_X" },
      },
      ["name"] = "Roll",
    },
    ["a2003cdnil"] = {
      ["removed"] = {
        [1] = { ["key"] = "JOY_RZ" },
      },
      ["name"] = "Rudder",
    },
    ["a3224cd3"] = {
      ["added"] = {
        [1] = { ["key"] = "JOY_Y", ["filter"] = { ["curvature"] = { [1] = 0 }, ["deadzone"] = 0, ["hardwareDetent"] = false, ["hardwareDetentAB"] = 0, ["hardwareDetentMax"] = 0, ["invert"] = true, ["saturationX"] = 1, ["saturationY"] = 1, ["slider"] = false } },
      },
      ["removed"] = {
        [1] = { ["key"] = "JOY_SLIDER1" },
      },
      ["name"] = "Propeller governor handle",
    },
    ["a3230cd3"] = {
      ["added"] = {
        [1] = { ["key"] = "JOY_Z" },
      },
      ["name"] = "Mixture handle",
    },
    ["a3236cd3"] = {
      ["added"] = {
        [1] = { ["key"] = "JOY_X" },
      },
      ["removed"] = {
        [1] = { ["key"] = "JOY_Z" },
      },
      ["name"] = "Throttle Lever",
    },
  },
}
return diff
