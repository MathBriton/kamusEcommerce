"use client";

import { useState } from "react";
import type { Facets } from "@/lib/catalog";
import { PRICE_RANGES, SORT_OPTIONS, type ListingState } from "./listing-state";

type Props = {
  facets: Facets | null;
  state: ListingState;
  onChange: (state: ListingState) => void;
};

const toggle = (list: string[], value: string) =>
  list.includes(value) ? list.filter((v) => v !== value) : [...list, value];

export function ListingFilters({ facets, state, onChange }: Props) {
  const priceValue = state.price ? `${state.price.min ?? ""}-${state.price.max ?? ""}` : "";
  const hasFilters = state.sizes.length > 0 || state.colors.length > 0 || state.price;
  const activeCount = state.sizes.length + state.colors.length + (state.price ? 1 : 0);
  const [open, setOpen] = useState(false);

  return (
    <div className="mb-8 lg:mb-0">
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        aria-expanded={open}
        aria-controls="listing-filters"
        className="w-full border border-ink py-3 text-sm tracking-wide uppercase lg:hidden"
      >
        Filtrar e ordenar{activeCount > 0 ? ` (${activeCount})` : ""}
      </button>
      <aside
        id="listing-filters"
        className={`${open ? "mt-6 block" : "hidden"} space-y-6 text-sm lg:block`}
        aria-label="Filtros"
      >
        <div>
          <label htmlFor="sort" className="mb-2 block font-medium">
            Ordenar por
          </label>
          <select
            id="sort"
            value={state.sort}
            onChange={(e) => onChange({ ...state, sort: e.target.value })}
            className="w-full border border-line bg-surface px-3 py-2"
          >
            {SORT_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>
                {o.label}
              </option>
            ))}
          </select>
        </div>

        {facets && facets.sizes.length > 0 && (
          <fieldset>
            <legend className="mb-2 font-medium">Tamanho</legend>
            <div className="flex flex-wrap gap-2">
              {facets.sizes.map((size) => {
                const active = state.sizes.includes(size);
                return (
                  <button
                    key={size}
                    type="button"
                    aria-pressed={active}
                    onClick={() => onChange({ ...state, sizes: toggle(state.sizes, size) })}
                    className={`min-w-11 border px-2 py-2 ${active ? "border-ink bg-ink text-paper" : "border-line bg-surface hover:border-ink"}`}
                  >
                    {size === "U" ? "Único" : size}
                  </button>
                );
              })}
            </div>
          </fieldset>
        )}

        {facets && facets.colors.length > 0 && (
          <fieldset>
            <legend className="mb-2 font-medium">Cor</legend>
            <ul className="space-y-1">
              {facets.colors.map((color) => (
                <li key={color.name}>
                  <label className="flex cursor-pointer items-center gap-2 py-1">
                    <input
                      type="checkbox"
                      checked={state.colors.includes(color.name)}
                      onChange={() =>
                        onChange({ ...state, colors: toggle(state.colors, color.name) })
                      }
                      className="accent-ink"
                    />
                    <span
                      className="size-4 rounded-full border border-black/15"
                      style={{ backgroundColor: color.hex }}
                      aria-hidden
                    />
                    {color.name}
                  </label>
                </li>
              ))}
            </ul>
          </fieldset>
        )}

        <fieldset>
          <legend className="mb-2 font-medium">Preço</legend>
          <ul className="space-y-1">
            {PRICE_RANGES.map((range) => (
              <li key={range.value}>
                <label className="flex cursor-pointer items-center gap-2 py-1">
                  <input
                    type="radio"
                    name="price"
                    checked={priceValue === range.value}
                    onChange={() => {
                      const [min, max] = range.value.split("-");
                      onChange({
                        ...state,
                        price: {
                          min: min ? Number(min) : undefined,
                          max: max ? Number(max) : undefined,
                        },
                      });
                    }}
                    className="accent-ink"
                  />
                  {range.label}
                </label>
              </li>
            ))}
          </ul>
        </fieldset>

        {hasFilters && (
          <button
            type="button"
            onClick={() => onChange({ sizes: [], colors: [], price: undefined, sort: state.sort })}
            className="text-accent underline underline-offset-4"
          >
            Limpar filtros
          </button>
        )}
      </aside>
    </div>
  );
}
