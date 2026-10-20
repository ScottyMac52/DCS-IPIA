local diff = {
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
      ["added"] = {
        [1] = { ["key"] = "JOY_Z" },
      },
      ["name"] = "Rudder",
    },
    ["a3236cd3"] = {
      ["removed"] = {
        [1] = { ["key"] = "JOY_Z" },
      },
      ["name"] = "Throttle Lever",
    },
    ["a3539cd7"] = {
      ["added"] = {
        [1] = { ["key"] = "JOY_Y", ["filter"] = { ["curvature"] = { [1] = 0 }, ["deadzone"] = 0, ["hardwareDetent"] = false, ["hardwareDetentAB"] = 0, ["hardwareDetentMax"] = 0, ["invert"] = true, ["saturationX"] = 1, ["saturationY"] = 1, ["slider"] = false } },
      },
      ["name"] = "Gear brake, left",
    },
    ["a3540cd7"] = {
      ["added"] = {
        [1] = { ["key"] = "JOY_X", ["filter"] = { ["curvature"] = { [1] = 0 }, ["deadzone"] = 0, ["hardwareDetent"] = false, ["hardwareDetentAB"] = 0, ["hardwareDetentMax"] = 0, ["invert"] = true, ["saturationX"] = 1, ["saturationY"] = 1, ["slider"] = false } },
      },
      ["name"] = "Gear brake, right",
    },
  },
}
return diff
