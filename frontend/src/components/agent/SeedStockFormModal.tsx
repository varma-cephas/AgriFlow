import React, { useEffect, useState } from 'react';
import { Package, X } from 'lucide-react';
import type { CropOption, SeedStockItem, UpsertSeedStockPayload } from '../../types/agent';

interface SeedStockFormModalProps {
  isOpen: boolean;
  farmerName: string;
  crops: CropOption[];
  editingItem: SeedStockItem | null;
  onClose: () => void;
  onSave: (payload: UpsertSeedStockPayload, existingId?: number) => Promise<void>;
}

export const SeedStockFormModal: React.FC<SeedStockFormModalProps> = ({
  isOpen,
  farmerName,
  crops,
  editingItem,
  onClose,
  onSave,
}) => {
  const [cropId, setCropId] = useState<number>(crops[0]?.id ?? 1);
  const [variety, setVariety] = useState('');
  const [supplier, setSupplier] = useState('');
  const [quantity, setQuantity] = useState('');
  const [unit, setUnit] = useState('kg');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!isOpen) return;
    setError(null);
    if (editingItem) {
      setCropId(editingItem.cropId);
      setVariety(editingItem.variety);
      setSupplier(editingItem.supplier ?? '');
      setQuantity(String(editingItem.quantity));
      setUnit(editingItem.unit);
    } else {
      setCropId(crops[0]?.id ?? 1);
      setVariety('');
      setSupplier('');
      setQuantity('');
      setUnit('kg');
    }
  }, [isOpen, editingItem, crops]);

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    const qtyNum = Number(quantity);
    if (Number.isNaN(qtyNum) || qtyNum < 0) {
      setError('Quantity must be 0 or greater.');
      return;
    }

    setSubmitting(true);
    try {
      await onSave(
        {
          cropId,
          variety: variety.trim(),
          supplier: supplier.trim() || null,
          quantity: qtyNum,
          unit,
        },
        editingItem?.id
      );
      onClose();
    } catch (err: any) {
      setError(err.response?.data?.message || 'Unable to save seed stock.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/45 backdrop-blur-[1px] p-4">
      <div className="relative w-full max-w-[440px] rounded-2xl bg-white p-6 shadow-xl border border-slate-100">
        <button
          type="button"
          onClick={onClose}
          className="absolute right-4 top-4 text-slate-400 hover:text-slate-600 cursor-pointer"
        >
          <X className="w-4 h-4" />
        </button>

        <div className="flex items-start gap-3">
          <div className="w-9 h-9 rounded-full bg-emerald-50 border border-emerald-200 flex items-center justify-center text-agri-700 shrink-0">
            <Package className="w-4 h-4" />
          </div>
          <div>
            <h3 className="text-base font-bold text-slate-900">
              {editingItem ? 'Edit seed stock' : 'Add seed stock'}
            </h3>
            <p className="text-xs text-slate-500 mt-0.5">
              Saved to {farmerName}&apos;s records and logged in the audit trail.
            </p>
          </div>
        </div>

        {error && (
          <div className="mt-4 rounded-lg bg-red-50 border border-red-200 p-2.5 text-xs text-red-700">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit} className="mt-5 space-y-3.5">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-medium text-slate-700 mb-1">Crop *</label>
              <select
                value={cropId}
                onChange={(e) => setCropId(Number(e.target.value))}
                className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-xs focus:border-agri-700 focus:outline-none"
              >
                {crops.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </select>
            </div>

            <div>
              <label className="block text-xs font-medium text-slate-700 mb-1">Variety *</label>
              <input
                type="text"
                required
                placeholder="e.g. Roma"
                value={variety}
                onChange={(e) => setVariety(e.target.value)}
                className="w-full rounded-lg border border-slate-200 px-3 py-2 text-xs focus:border-agri-700 focus:outline-none"
              />
            </div>
          </div>

          <div>
            <label className="block text-xs font-medium text-slate-700 mb-1">
              Supplier (optional)
            </label>
            <input
              type="text"
              placeholder="e.g. Agro-Input Ltd"
              value={supplier}
              onChange={(e) => setSupplier(e.target.value)}
              className="w-full rounded-lg border border-slate-200 px-3 py-2 text-xs focus:border-agri-700 focus:outline-none"
            />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-medium text-slate-700 mb-1">Quantity *</label>
              <input
                type="number"
                step="0.01"
                min="0"
                required
                placeholder="25"
                value={quantity}
                onChange={(e) => setQuantity(e.target.value)}
                className="w-full rounded-lg border border-slate-200 px-3 py-2 text-xs focus:border-agri-700 focus:outline-none"
              />
            </div>

            <div>
              <label className="block text-xs font-medium text-slate-700 mb-1">Unit *</label>
              <select
                value={unit}
                onChange={(e) => setUnit(e.target.value)}
                className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-xs focus:border-agri-700 focus:outline-none"
              >
                <option value="kg">kg</option>
                <option value="g">g</option>
                <option value="packets">packets</option>
                <option value="tubers">tubers</option>
              </select>
            </div>
          </div>

          <div className="pt-2 flex items-center justify-end gap-2.5">
            <button
              type="button"
              onClick={onClose}
              className="rounded-lg border border-slate-200 bg-white hover:bg-slate-50 text-slate-700 font-medium text-xs px-4 py-2 transition cursor-pointer"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={submitting}
              className="rounded-lg bg-agri-700 hover:bg-agri-800 disabled:opacity-60 text-white font-semibold text-xs px-4 py-2 transition cursor-pointer"
            >
              {submitting ? 'Saving...' : editingItem ? 'Save changes' : 'Add seed stock'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};