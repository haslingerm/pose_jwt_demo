export class LocalStorage {
  constructor(private readonly prefix: string) {
  }

  public set<T>(key: string, value: T): void {
    localStorage.setItem(this.getKey(key), JSON.stringify(value));
  }

  public get(key: string): unknown {
    const item = localStorage.getItem(this.getKey(key));
    if (item === null) {
      return null;
    }

    try {
      return JSON.parse(item);
    } catch {
      // corrupted (or manually edited) value - treat it as missing
      return null;
    }
  }

  public remove(key: string): void {
    localStorage.removeItem(this.getKey(key));
  }

  private getKey(key: string): string {
    return `${this.prefix}.${key}`;
  }
}
