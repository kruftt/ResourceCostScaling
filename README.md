# Resource Cost Scaling

- Scales all piece and crafting resource costs using a single configuration value.
- Rounds toward nearest positive integer
- Midpoints round to even numbers
- Setting to 0 does not discover new pieces or recipes.

<br/>

```
2 x 1.25 = 2.50 -> 2
2 x 1.30 = 2.60 -> 3
```
```
2 x 1.70 = 3.40 -> 3
2 x 1.75 = 3.50 -> 4
```
```
2 x 0.01 = 0.02 -> 1
2 x 0.00 = 0.00 -> 0
```

<br/>  
  
[Scaling Image](https://github.com/kruftt/ResourceCostScaling/blob/master/bundle/scaling.png)

<br/>

---

[ServerSync enabled](https://github.com/blaxxun-boop/ServerSync)  
[Valheim Modding Server](https://discord.com/invite/89bBsvK5KC)  
[Thunderstore](https://thunderstore.io/c/valheim/p/kruft/ResourceCostScaling/)
[Github](https://github.com/kruftt/ResourceCostScaling)  

`Discord` Kruft#6332  
